using Microsoft.EntityFrameworkCore;

namespace Repositories.Implements;

/// <summary>
/// Teammate search queries implementation.
/// Uses indexed columns (User.University, UserGame.GameId, UserGame.Skill) for efficient filtering.
/// Computes SharedGames by comparing current user's game list with candidates.
/// Stable sorting: Points DESC, SharedGames DESC, UserId DESC (for deterministic pagination).
/// </summary>
public sealed class TeammateQueryRepository : ITeammateQueryRepository
{
    private readonly AppDbContext _context;

    public TeammateQueryRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<(IReadOnlyList<TeammateCandidate> Candidates, string? NextCursor)>
        SearchCandidatesAsync(
            Guid currentUserId,
            TeammateSearchFilter filter,
            CursorRequest cursor,
            CancellationToken ct = default)
    {
        // 1) Get current user's game IDs for SharedGames calculation
        var myGameIds = await _context.UserGames
            .Where(ug => ug.UserId == currentUserId)
            .Select(ug => ug.GameId)
            .Distinct()
            .ToListAsync(ct);

        // 2) Base query: users who are not soft-deleted and not the current user
        var baseQuery = _context.Users
            .Where(u => u.Id != currentUserId);

        // 3) Apply filters by joining with UserGames when needed
        IQueryable<User> filteredUsers;

        if (filter.GameId.HasValue || filter.Skill.HasValue)
        {
            // Need to join with UserGames for game/skill filtering
            filteredUsers = baseQuery
                .Where(u => u.UserGames.Any(ug =>
                    (!filter.GameId.HasValue || ug.GameId == filter.GameId.Value) &&
                    (!filter.Skill.HasValue || ug.Skill == filter.Skill.Value)
                ));
        }
        else
        {
            filteredUsers = baseQuery;
        }

        // 4) Apply university filter (indexed column)
        if (!string.IsNullOrWhiteSpace(filter.University))
        {
            filteredUsers = filteredUsers.Where(u => u.University == filter.University);
        }

        // 5) Project to intermediate result with SharedGames calculation
        var projected = filteredUsers
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.AvatarUrl,
                u.University,
                u.Points,
                // Count distinct games shared with current user
                SharedGames = u.UserGames
                    .Where(ug => myGameIds.Contains(ug.GameId))
                    .Select(ug => ug.GameId)
                    .Distinct()
                    .Count()
            });

        // 6) Stable sorting: Points DESC, SharedGames DESC, UserId DESC
        var ordered = projected
            .OrderByDescending(x => x.Points)
            .ThenByDescending(x => x.SharedGames)
            .ThenByDescending(x => x.Id);

        // 7) Cursor pagination
        //    Cursor format: "points:sharedGames:userId"
        //    For simplicity, we'll use basic Skip/Take with cursor as a composite key
        IQueryable<object> paged;

        if (!string.IsNullOrWhiteSpace(cursor.Cursor))
        {
            var parts = cursor.Cursor.Split(':');
            if (parts.Length == 3 &&
                int.TryParse(parts[0], out var cursorPoints) &&
                int.TryParse(parts[1], out var cursorShared) &&
                Guid.TryParse(parts[2], out var cursorId))
            {
                // Filter to get items after the cursor
                // Since we sort DESC, "after" means:
                // (Points < cursorPoints) OR
                // (Points == cursorPoints AND SharedGames < cursorShared) OR
                // (Points == cursorPoints AND SharedGames == cursorShared AND Id < cursorId)
                paged = ordered.Where(x =>
                    x.Points < cursorPoints ||
                    (x.Points == cursorPoints && x.SharedGames < cursorShared) ||
                    (x.Points == cursorPoints && x.SharedGames == cursorShared && x.Id.CompareTo(cursorId) < 0)
                );
            }
            else
            {
                paged = ordered;
            }
        }
        else
        {
            paged = ordered;
        }

        // 8) Take (size + 1) to detect if there's a next page
        var size = cursor.SizeSafe;
        var items = await ((IQueryable<dynamic>)paged)
            .Take(size + 1)
            .ToListAsync(ct);

        var hasMore = items.Count > size;
        if (hasMore)
        {
            items = items.Take(size).ToList();
        }

        // 9) Map to TeammateCandidate
        var candidates = items.Select(x => new TeammateCandidate(
            UserId: (Guid)x.Id,
            FullName: (string?)x.FullName,
            AvatarUrl: (string?)x.AvatarUrl,
            University: (string?)x.University,
            Points: (int)x.Points,
            SharedGames: (int)x.SharedGames
        )).ToList();

        // 10) Generate next cursor
        string? nextCursor = null;
        if (hasMore && candidates.Count > 0)
        {
            var last = candidates[^1];
            nextCursor = $"{last.Points}:{last.SharedGames}:{last.UserId}";
        }

        return (candidates, nextCursor);
    }
}
