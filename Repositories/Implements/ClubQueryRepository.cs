using BusinessObjects.Common.Pagination;
using DTOs.Common.Filters;
using Microsoft.EntityFrameworkCore;
using Repositories.Models;

namespace Repositories.Implements;

/// <summary>
/// Club query implementation using cursor-based pagination.
/// Uses indexes on CommunityId, MembersCount for performance.
/// Respects soft-delete global filters.
/// </summary>
public sealed class ClubQueryRepository : IClubQueryRepository
{
    private readonly AppDbContext _context;

    public ClubQueryRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Search clubs within a community with filtering and cursor-based pagination.
    /// Stable sort order: MembersCount DESC, Id DESC
    /// </summary>
    public async Task<(IReadOnlyList<Club> Items, string? NextCursor)> SearchClubsAsync(
        Guid communityId,
        string? name,
        bool? isPublic,
        int? membersFrom,
        int? membersTo,
        CursorRequest cursor,
        CancellationToken ct = default)
    {
        // Base query: filter by community ID
        IQueryable<Club> query = _context.Clubs
            .AsNoTracking()
            .Where(c => c.CommunityId == communityId);

        // Apply filters
        if (!string.IsNullOrWhiteSpace(name))
        {
            var normalizedName = name.Trim().ToUpperInvariant();
            query = query.Where(c => c.Name.ToUpper().Contains(normalizedName));
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

    public async Task<bool> AnyByCommunityAsync(Guid communityId, CancellationToken ct = default)
    {
        return await _context.Clubs
            .AnyAsync(c => c.CommunityId == communityId, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Check if a club still has approved room members.
    /// </summary>
    public async Task<bool> HasAnyApprovedRoomsAsync(Guid clubId, CancellationToken ct = default)
    {
        return await _context.RoomMembers
            .AsNoTracking()
            .AnyAsync(rm =>
                rm.Status == RoomMemberStatus.Approved &&
                rm.Room!.ClubId == clubId,
                ct);
    }

    /// <summary>
    /// Get club by ID.
    /// </summary>
    public async Task<Club?> GetByIdAsync(Guid clubId, CancellationToken ct = default)
    {
        return await _context.Clubs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == clubId, ct);
    }

    public async Task<ClubMember?> GetMemberAsync(Guid clubId, Guid userId, CancellationToken ct = default)
    {
        return await _context.ClubMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(cm => cm.ClubId == clubId && cm.UserId == userId, ct);
    }

    public async Task<ClubDetailModel?> GetDetailsAsync(Guid clubId, Guid? currentUserId, CancellationToken ct = default)
    {
        var query = _context.Clubs
            .AsNoTracking()
            .Where(c => c.Id == clubId)
            .Select(c => new ClubDetailModel(
                c.Id,
                c.CommunityId,
                c.Name,
                c.Description,
                c.IsPublic,
                c.MembersCount,
                c.Rooms.Count(),
                _context.ClubMembers
                    .Where(cm => cm.ClubId == c.Id && cm.Role == MemberRole.Owner)
                    .Select(cm => cm.UserId)
                    .FirstOrDefault(),
                currentUserId.HasValue && _context.ClubMembers
                    .Any(cm => cm.ClubId == c.Id && cm.UserId == currentUserId.Value),
                currentUserId.HasValue && _context.CommunityMembers
                    .Any(cm => cm.CommunityId == c.CommunityId && cm.UserId == currentUserId.Value),
                currentUserId.HasValue && _context.ClubMembers
                    .Any(cm => cm.ClubId == c.Id && cm.UserId == currentUserId.Value && cm.Role == MemberRole.Owner),
                c.CreatedAtUtc,
                c.UpdatedAtUtc
            ));

        return await query.FirstOrDefaultAsync(ct);
    }

    public async Task<OffsetPage<ClubMemberModel>> ListMembersAsync(
        Guid clubId,
        MemberListFilter filter,
        OffsetPaging paging,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        filter.Normalize();

        var offset = paging.OffsetSafe;
        var limit = Math.Clamp(paging.LimitSafe, 1, 50);

        var query = _context.ClubMembers
            .AsNoTracking()
            .Where(cm => cm.ClubId == clubId)
            .Join(
                _context.Users.AsNoTracking(),
                cm => cm.UserId,
                u => u.Id,
                (cm, u) => new
                {
                    cm.Role,
                    cm.JoinedAt,
                    UserId = u.Id,
                    UserName = u.UserName ?? string.Empty,
                    u.FullName,
                    u.AvatarUrl,
                    u.Level
                });

        if (filter.Role.HasValue)
        {
            var role = filter.Role.Value;
            query = query.Where(x => x.Role == role);
        }

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var pattern = $"%{filter.Query!}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.FullName ?? string.Empty, pattern) ||
                EF.Functions.ILike(x.UserName, pattern));
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);

        var ordered = filter.Sort switch
        {
            MemberListSort.NameAsc => query
                .OrderBy(x => x.FullName ?? x.UserName)
                .ThenBy(x => x.UserName)
                .ThenByDescending(x => x.JoinedAt),
            MemberListSort.NameDesc => query
                .OrderByDescending(x => x.FullName ?? x.UserName)
                .ThenByDescending(x => x.UserName)
                .ThenByDescending(x => x.JoinedAt),
            MemberListSort.Role => query
                .OrderBy(x => x.Role == MemberRole.Owner ? 0 : x.Role == MemberRole.Admin ? 1 : 2)
                .ThenByDescending(x => x.JoinedAt)
                .ThenBy(x => x.UserId),
            _ => query
                .OrderByDescending(x => x.JoinedAt)
                .ThenBy(x => x.UserId)
        };

        var items = await ordered
            .Skip(offset)
            .Take(limit)
            .Select(x => new ClubMemberModel(
                new MemberUserModel(x.UserId, x.UserName, x.FullName, x.AvatarUrl, x.Level),
                x.Role,
                x.JoinedAt))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var hasNext = offset + items.Count < total;
        var hasPrev = offset > 0;

        return new OffsetPage<ClubMemberModel>(items, offset, limit, total, hasPrev, hasNext);
    }

    public async Task<IReadOnlyList<ClubMemberModel>> ListRecentMembersAsync(
        Guid clubId,
        int limit,
        CancellationToken ct = default)
    {
        var sanitizedLimit = Math.Clamp(limit, 1, 50);

        var query = _context.ClubMembers
            .AsNoTracking()
            .Where(cm => cm.ClubId == clubId)
            .Join(
                _context.Users.AsNoTracking(),
                cm => cm.UserId,
                u => u.Id,
                (cm, u) => new
                {
                    cm.Role,
                    cm.JoinedAt,
                    UserId = u.Id,
                    UserName = u.UserName ?? string.Empty,
                    u.FullName,
                    u.AvatarUrl,
                    u.Level
                })
            .OrderByDescending(x => x.JoinedAt)
            .ThenBy(x => x.UserId)
            .Take(sanitizedLimit)
            .Select(x => new ClubMemberModel(
                new MemberUserModel(x.UserId, x.UserName, x.FullName, x.AvatarUrl, x.Level),
                x.Role,
                x.JoinedAt));

        return await query.ToListAsync(ct).ConfigureAwait(false);
    }

}
