using Microsoft.EntityFrameworkCore;
using Repositories.Models;

namespace Repositories.Implements;

/// <summary>
/// Community query implementation using cursor-based pagination.
/// Uses indexes on School, IsPublic, MembersCount for performance.
/// Respects soft-delete global filters.
/// </summary>
public sealed class CommunityQueryRepository : ICommunityQueryRepository
{
    private readonly AppDbContext _context;

    public CommunityQueryRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Community?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Communities
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            .ConfigureAwait(false);

    public async Task<CommunityMember?> GetMemberAsync(Guid communityId, Guid userId, CancellationToken ct = default)
        => await _context.CommunityMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(cm => cm.CommunityId == communityId && cm.UserId == userId, ct)
            .ConfigureAwait(false);

    public async Task<CommunityDetailModel?> GetDetailsAsync(Guid communityId, Guid? currentUserId, CancellationToken ct = default)
    {
        var query = _context.Communities
            .AsNoTracking()
            .Where(c => c.Id == communityId)
            .Select(c => new CommunityDetailModel(
                c.Id,
                c.Name,
                c.Description,
                c.School,
                c.IsPublic,
                c.MembersCount,
                c.Clubs.Count(),
                _context.CommunityMembers
                    .Where(cm => cm.CommunityId == c.Id && cm.Role == MemberRole.Owner)
                    .Select(cm => cm.UserId)
                    .FirstOrDefault(),
                currentUserId.HasValue && _context.CommunityMembers
                    .Any(cm => cm.CommunityId == c.Id && cm.UserId == currentUserId.Value),
                currentUserId.HasValue && _context.CommunityMembers
                    .Any(cm => cm.CommunityId == c.Id && cm.UserId == currentUserId.Value && cm.Role == MemberRole.Owner),
                c.CreatedAtUtc,
                c.UpdatedAtUtc
            ));

        return await query.FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> HasAnyApprovedRoomsAsync(Guid communityId, CancellationToken ct = default)
        => await _context.RoomMembers
            .AsNoTracking()
            .AnyAsync(
                rm => rm.Status == RoomMemberStatus.Approved
                      && rm.Room!.Club!.CommunityId == communityId,
                ct)
            .ConfigureAwait(false);

    /// <summary>
    /// Search communities with filtering and cursor-based pagination.
    /// Stable sort order: MembersCount DESC, Id DESC
    /// </summary>
    public async Task<(IReadOnlyList<Community> Items, string? NextCursor)> SearchCommunitiesAsync(
        string? school,
        Guid? gameId,
        bool? isPublic,
        int? membersFrom,
        int? membersTo,
        CursorRequest cursor,
        CancellationToken ct = default)
    {
        IQueryable<Community> query = _context.Communities.AsNoTracking();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(school))
        {
            var normalizedSchool = school.Trim().ToUpperInvariant();
            query = query.Where(c => (c.School ?? string.Empty).ToUpper().Contains(normalizedSchool));
        }

        if (gameId.HasValue)
        {
            // Filter communities that have this game
            query = query.Where(c => c.Games.Any(cg => cg.GameId == gameId.Value));
        }

        if (isPublic.HasValue)
        {
            query = query.Where(c => c.IsPublic == isPublic.Value);
        }

        if (membersFrom.HasValue)
        {
            query = query.Where(c => c.MembersCount >= membersFrom.Value);
        }

        if (membersTo.HasValue)
        {
            query = query.Where(c => c.MembersCount <= membersTo.Value);
        }

        // Apply cursor-based pagination with stable sorting
        // Sort by MembersCount DESC, then Id DESC for stability
        // Use Id as the cursor key for simplicity (stable and unique)
        var orderedQuery = query
            .OrderByDescending(c => c.MembersCount)
            .ThenByDescending(c => c.Id);

        var result = await orderedQuery.ToCursorPageAsync(
            cursor,
            c => c.Id, // Use Id as cursor key (stable, unique)
            ct
        );

        return (result.Items, result.NextCursor);
    }
}
