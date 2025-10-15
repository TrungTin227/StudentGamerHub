using BusinessObjects;
using BusinessObjects.Common.Results;
using DTOs.Communities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Repositories.Persistence;
using System.Text;

namespace Services.Implementations;

/// <summary>
/// Community discovery service with popularity scoring.
/// Uses efficient index-backed queries and composite cursor for stable pagination.
/// </summary>
public sealed class CommunityDiscoveryService : ICommunityDiscoveryService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CommunityDiscoveryService> _logger;

    public CommunityDiscoveryService(
        AppDbContext db,
        ILogger<CommunityDiscoveryService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<DiscoverResponse>> DiscoverAsync(
        string? school,
        Guid? gameId,
        string? cursor,
        int? size,
        CancellationToken ct = default)
    {
        try
        {
            // Clamp size
            var pageSize = Math.Clamp(size ?? 20, 1, 100);
            
            // Calculate 48h window (align with RoomMember.JoinedAt type)
            var since = DateTimeOffset.UtcNow.AddHours(-48);

            // Base query: IsPublic = true
            IQueryable<Community> baseQuery = _db.Communities
                .AsNoTracking()
                .Where(c => c.IsPublic);

            // Filter by school (case-insensitive exact match)
            if (!string.IsNullOrWhiteSpace(school))
            {
                var normalizedSchool = school.Trim();
                baseQuery = baseQuery.Where(c => c.School != null && c.School.ToLower() == normalizedSchool.ToLower());
            }

            // Filter by gameId
            if (gameId.HasValue)
            {
                baseQuery = baseQuery.Where(c => c.Games.Any(g => g.GameId == gameId.Value));
            }

            // Project with correlated subquery for recent activity (last 48h joins) per community
            // Use SelectMany + Count(predicate) for better translation
            var projected = baseQuery.Select(c => new DiscoveryProjection
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                School = c.School,
                IsPublic = c.IsPublic,
                MembersCount = c.MembersCount,
                CreatedAtUtc = c.CreatedAtUtc,
                RecentActivity48h = _db.Rooms
                    .Where(r => r.Club!.CommunityId == c.Id)
                    .SelectMany(r => r.Members)
                    .Count(rm => rm.JoinedAt >= since)
            });

            // Sort by popularity: MembersCount DESC, RecentActivity48h DESC, CreatedAtUtc DESC, Id ASC
            var ordered = projected
                .OrderByDescending(x => x.MembersCount)
                .ThenByDescending(x => x.RecentActivity48h)
                .ThenByDescending(x => x.CreatedAtUtc)
                .ThenBy(x => x.Id);

            // Apply cursor filter if provided
            IQueryable<DiscoveryProjection> filtered = ordered;
            if (!string.IsNullOrWhiteSpace(cursor))
            {
                var parsedCursor = ParseCursor(cursor);
                if (parsedCursor != null)
                {
                    // Filter items after the cursor position
                    // For multi-key sorting, we need complex comparison:
                    // (M1, R1, C1, I1) > (M2, R2, C2, I2) if:
                    //   M1 < M2 OR
                    //   (M1 == M2 AND R1 < R2) OR
                    //   (M1 == M2 AND R1 == R2 AND C1 < C2) OR
                    //   (M1 == M2 AND R1 == R2 AND C1 == C2 AND I1 > I2)
                    filtered = ordered.Where(x =>
                        x.MembersCount < parsedCursor.MembersCount ||
                        (x.MembersCount == parsedCursor.MembersCount && x.RecentActivity48h < parsedCursor.RecentActivity48h) ||
                        (x.MembersCount == parsedCursor.MembersCount && x.RecentActivity48h == parsedCursor.RecentActivity48h && x.CreatedAtUtc < parsedCursor.CreatedAtUtc) ||
                        (x.MembersCount == parsedCursor.MembersCount && x.RecentActivity48h == parsedCursor.RecentActivity48h && x.CreatedAtUtc == parsedCursor.CreatedAtUtc && x.Id.CompareTo(parsedCursor.Id) > 0)
                    );
                }
            }

            // Take pageSize + 1 to determine if there's a next page
            var items = await filtered
                .Take(pageSize + 1)
                .ToListAsync(ct);

            var hasMore = items.Count > pageSize;
            if (hasMore)
            {
                items = items.Take(pageSize).ToList();
            }

            // Map to DTOs
            var dtos = items.Select(x => new CommunityDiscoverDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                School = x.School,
                IsPublic = x.IsPublic,
                MembersCount = x.MembersCount,
                RecentActivity48h = x.RecentActivity48h,
                CreatedAtUtc = x.CreatedAtUtc
            }).ToList();

            // Build next cursor if there are more pages
            string? nextCursor = null;
            if (hasMore && dtos.Count > 0)
            {
                var last = dtos[^1];
                nextCursor = BuildCursor(last.MembersCount, last.RecentActivity48h, last.CreatedAtUtc, last.Id);
            }

            var response = new DiscoverResponse
            {
                Items = dtos,
                NextCursor = nextCursor
            };

            return Result<DiscoverResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering communities");
            return Result<DiscoverResponse>.Failure(new Error(Error.Codes.Unexpected, "Failed to discover communities."));
        }
    }

    // Composite cursor format: "{MembersCount:D10}|{RecentActivity:D10}|{CreatedAtTicks:D19}|{Id:N}"
    private static string BuildCursor(int membersCount, int recentActivity, DateTime createdAtUtc, Guid id)
    {
        // Invert ticks for DESC sorting (larger values = earlier in sequence)
        var invertedTicks = long.MaxValue - createdAtUtc.Ticks;
        
        var raw = $"{membersCount:D10}|{recentActivity:D10}|{invertedTicks:D19}|{id:N}";
        var bytes = Encoding.UTF8.GetBytes(raw);
        return Convert.ToBase64String(bytes);
    }

    private static DiscoverCursor? ParseCursor(string cursor)
    {
        try
        {
            var bytes = Convert.FromBase64String(cursor);
            var raw = Encoding.UTF8.GetString(bytes);
            var parts = raw.Split('|');
            
            if (parts.Length != 4)
                return null;

            var membersCount = int.Parse(parts[0]);
            var recentActivity = int.Parse(parts[1]);
            var invertedTicks = long.Parse(parts[2]);
            var createdAtTicks = long.MaxValue - invertedTicks;
            var id = Guid.Parse(parts[3]);

            return new DiscoverCursor
            {
                MembersCount = membersCount,
                RecentActivity48h = recentActivity,
                CreatedAtUtc = new DateTime(createdAtTicks, DateTimeKind.Utc),
                Id = id
            };
        }
        catch
        {
            return null;
        }
    }

    private sealed class DiscoveryProjection
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public string? School { get; set; }
        public bool IsPublic { get; set; }
        public int MembersCount { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public int RecentActivity48h { get; set; }
    }

    private sealed class DiscoverCursor
    {
        public int MembersCount { get; set; }
        public int RecentActivity48h { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public Guid Id { get; set; }
    }
}
