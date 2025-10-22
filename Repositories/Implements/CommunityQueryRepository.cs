using BusinessObjects.Common.Pagination;
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

    public async Task<PagedResult<CommunityDetailModel>> SearchDiscoverAsync(
        Guid? currentUserId,
        string? query,
        bool orderByTrending,
        PageRequest paging,
        CancellationToken ct = default)
    {
        var normalizedQuery = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
        var page = paging.PageSafe;
        var size = Math.Clamp(paging.SizeSafe, 1, 50);

        IQueryable<Community> baseQuery = _context.Communities.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            var term = normalizedQuery.ToUpperInvariant();
            baseQuery = baseQuery.Where(c =>
                c.Name.ToUpper().Contains(term) ||
                (c.Description ?? string.Empty).ToUpper().Contains(term));
        }

        var total = await baseQuery.CountAsync(ct).ConfigureAwait(false);

        var projection = baseQuery.Select(c => new CommunityDetailModel(
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

        IOrderedQueryable<CommunityDetailModel> ordered = orderByTrending
            ? projection
                .OrderByDescending(c => c.MembersCount)
                .ThenByDescending(c => c.ClubsCount)
                .ThenByDescending(c => c.CreatedAtUtc)
                .ThenBy(c => c.Id)
            : projection
                .OrderByDescending(c => c.CreatedAtUtc)
                .ThenByDescending(c => c.MembersCount)
                .ThenBy(c => c.Id);

        var skip = (page - 1) * size;
        var items = await ordered
            .Skip(skip)
            .Take(size)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)size);
        var hasPrev = page > 1 && total > 0;
        var hasNext = totalPages > 0 && page < totalPages;
        var sortLabel = orderByTrending ? nameof(Community.MembersCount) : nameof(Community.CreatedAtUtc);

        return new PagedResult<CommunityDetailModel>(
            items,
            page,
            size,
            total,
            totalPages,
            hasPrev,
            hasNext,
            sortLabel,
            desc: true);
    }
}
