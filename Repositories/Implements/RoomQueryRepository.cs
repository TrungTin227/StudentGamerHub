using BusinessObjects;
using BusinessObjects.Common.Pagination;
using System.Collections.Generic;
using DTOs.Common.Filters;
using Microsoft.EntityFrameworkCore;
using Repositories.Models;

namespace Repositories.Implements;

/// <summary>
/// Room query implementation for read operations.
/// Uses indexes on ClubId, JoinPolicy, Status, UserId for performance.
/// Respects soft-delete global filters.
/// </summary>
public sealed class RoomQueryRepository : IRoomQueryRepository
{
    private readonly AppDbContext _context;

    public RoomQueryRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Get room with Club and Community loaded via joins.
    /// </summary>
    public async Task<Room?> GetRoomWithClubCommunityAsync(Guid roomId, CancellationToken ct = default)
    {
        return await _context.Rooms
            .AsNoTracking()
            .Include(r => r.Club)
                .ThenInclude(c => c.Community)
            .FirstOrDefaultAsync(r => r.Id == roomId, ct);
    }

    /// <summary>
    /// Get room tracked for updates, including Club and Community navigations.
    /// </summary>
    public async Task<Room?> GetByIdAsync(Guid roomId, CancellationToken ct = default)
    {
        return await _context.Rooms
            .Include(r => r.Club)
                .ThenInclude(c => c.Community)
            .FirstOrDefaultAsync(r => r.Id == roomId, ct);
    }

    /// <summary>
    /// Get specific room member.
    /// </summary>
    public async Task<RoomMember?> GetMemberAsync(Guid roomId, Guid userId, CancellationToken ct = default)
    {
        return await _context.RoomMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(rm => rm.RoomId == roomId && rm.UserId == userId, ct);
    }

    public async Task<RoomDetailModel?> GetDetailsAsync(Guid roomId, Guid? currentUserId, CancellationToken ct = default)
    {
        var query = _context.Rooms
            .AsNoTracking()
            .Where(r => r.Id == roomId)
            .Select(r => new RoomDetailModel(
                r.Id,
                r.ClubId,
                r.Name,
                r.Description,
                r.JoinPolicy,
                r.Capacity,
                r.MembersCount,
                _context.RoomMembers
                    .Where(rm => rm.RoomId == r.Id && rm.Role == RoomRole.Owner)
                    .Select(rm => rm.UserId)
                    .FirstOrDefault(),
                currentUserId.HasValue && _context.RoomMembers
                    .Any(rm => rm.RoomId == r.Id && rm.UserId == currentUserId.Value && rm.Status == RoomMemberStatus.Approved),
                currentUserId.HasValue && _context.RoomMembers
                    .Any(rm => rm.RoomId == r.Id && rm.UserId == currentUserId.Value && rm.Status == RoomMemberStatus.Approved && rm.Role == RoomRole.Owner),
                currentUserId.HasValue
                    ? _context.RoomMembers
                        .Where(rm => rm.RoomId == r.Id && rm.UserId == currentUserId.Value)
                        .Select(rm => (RoomMemberStatus?)rm.Status)
                        .FirstOrDefault()
                    : null,
                r.CreatedAtUtc,
                r.UpdatedAtUtc
            ));

        return await query.FirstOrDefaultAsync(ct);
    }

    public async Task<PagedResult<RoomDetailModel>> ListByClubAsync(Guid clubId, Guid? currentUserId, PageRequest paging, CancellationToken ct = default)
    {
        var requestedSort = string.IsNullOrWhiteSpace(paging.Sort) ? nameof(RoomDetailModel.CreatedAtUtc) : paging.Sort!;

        var sortMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(RoomDetailModel.Name)] = nameof(Room.Name),
            [nameof(RoomDetailModel.Description)] = nameof(Room.Description),
            [nameof(RoomDetailModel.JoinPolicy)] = nameof(Room.JoinPolicy),
            [nameof(RoomDetailModel.Capacity)] = nameof(Room.Capacity),
            [nameof(RoomDetailModel.MembersCount)] = nameof(Room.MembersCount),
            [nameof(RoomDetailModel.CreatedAtUtc)] = nameof(Room.CreatedAtUtc),
            [nameof(RoomDetailModel.UpdatedAtUtc)] = nameof(Room.UpdatedAtUtc)
        };

        if (!sortMappings.TryGetValue(requestedSort, out var roomSort))
        {
            roomSort = nameof(Room.CreatedAtUtc);
        }

        var sanitized = new PageRequest(
            Page: paging.PageSafe,
            Size: Math.Clamp(paging.SizeSafe, 1, 50),
            Sort: roomSort,
            Desc: paging.Desc);

        var roomsQuery = _context.Rooms
            .AsNoTracking()
            .Where(r => !r.IsDeleted && r.ClubId == clubId);

        var pagedRooms = await roomsQuery
            .ToPagedResultAsync(sanitized, ct)
            .ConfigureAwait(false);

        if (pagedRooms.Items.Count == 0)
        {
            return new PagedResult<RoomDetailModel>(
                Array.Empty<RoomDetailModel>(),
                pagedRooms.Page,
                pagedRooms.Size,
                pagedRooms.TotalCount,
                pagedRooms.TotalPages,
                pagedRooms.HasPrevious,
                pagedRooms.HasNext,
                pagedRooms.Sort,
                pagedRooms.Desc);
        }

        var roomIds = pagedRooms.Items.Select(r => r.Id).ToArray();

        var owners = await _context.RoomMembers
            .AsNoTracking()
            .Where(rm => roomIds.Contains(rm.RoomId) && !rm.IsDeleted && rm.Role == RoomRole.Owner)
            .GroupBy(rm => rm.RoomId)
            .Select(g => new
            {
                RoomId = g.Key,
                OwnerId = g
                    .OrderBy(rm => rm.JoinedAt)
                    .Select(rm => rm.UserId)
                    .FirstOrDefault()
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var ownersMap = owners.ToDictionary(x => x.RoomId, x => x.OwnerId);

        Dictionary<Guid, (RoomMemberStatus Status, RoomRole Role)> membershipMap = new();

        if (currentUserId.HasValue)
        {
            var memberships = await _context.RoomMembers
                .AsNoTracking()
                .Where(rm => roomIds.Contains(rm.RoomId) && !rm.IsDeleted && rm.UserId == currentUserId.Value)
                .Select(rm => new
                {
                    rm.RoomId,
                    rm.Status,
                    rm.Role
                })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            membershipMap = memberships
                .ToDictionary(
                    x => x.RoomId,
                    x => (x.Status, x.Role));
        }

        var items = pagedRooms.Items
            .Select(r =>
            {
                var hasMember = membershipMap.TryGetValue(r.Id, out var memberInfo);

                var ownerId = ownersMap.TryGetValue(r.Id, out var foundOwner)
                    ? foundOwner
                    : Guid.Empty;

                var isMember = hasMember && memberInfo.Status == RoomMemberStatus.Approved;
                var isOwner = isMember && memberInfo.Role == RoomRole.Owner;
                var membershipStatus = hasMember ? memberInfo.Status : (RoomMemberStatus?)null;

                return new RoomDetailModel(
                    r.Id,
                    r.ClubId,
                    r.Name,
                    r.Description,
                    r.JoinPolicy,
                    r.Capacity,
                    r.MembersCount,
                    ownerId,
                    isMember,
                    isOwner,
                    membershipStatus,
                    r.CreatedAtUtc,
                    r.UpdatedAtUtc);
            })
            .ToList();

        return new PagedResult<RoomDetailModel>(
            items,
            pagedRooms.Page,
            pagedRooms.Size,
            pagedRooms.TotalCount,
            pagedRooms.TotalPages,
            pagedRooms.HasPrevious,
            pagedRooms.HasNext,
            pagedRooms.Sort,
            pagedRooms.Desc);
    }

    public async Task<bool> AnyByClubAsync(Guid clubId, CancellationToken ct = default)
    {
        return await _context.Rooms
            .AnyAsync(r => r.ClubId == clubId, ct)
            .ConfigureAwait(false);
    }

    public async Task<OffsetPage<RoomMemberModel>> ListMembersAsync(
        Guid roomId,
        RoomMemberListFilter filter,
        OffsetPaging paging,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        filter.Normalize();

        var offset = paging.OffsetSafe;
        var limit = Math.Clamp(paging.LimitSafe, 1, 50);

        var query = _context.RoomMembers
            .AsNoTracking()
            .Where(rm => rm.RoomId == roomId)
            .Join(
                _context.Users.AsNoTracking(),
                rm => rm.UserId,
                u => u.Id,
                (rm, u) => new
                {
                    rm.Role,
                    rm.Status,
                    rm.JoinedAt,
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

        if (filter.Status.HasValue)
        {
            var status = filter.Status.Value;
            query = query.Where(x => x.Status == status);
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
                .OrderBy(x => x.Role == RoomRole.Owner ? 0 : x.Role == RoomRole.Moderator ? 1 : 2)
                .ThenByDescending(x => x.JoinedAt)
                .ThenBy(x => x.UserId),
            _ => query
                .OrderByDescending(x => x.JoinedAt)
                .ThenBy(x => x.UserId)
        };

        var items = await ordered
            .Skip(offset)
            .Take(limit)
            .Select(x => new RoomMemberModel(
                new MemberUserModel(x.UserId, x.UserName, x.FullName, x.AvatarUrl, x.Level),
                x.Role,
                x.Status,
                x.JoinedAt))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var hasNext = offset + items.Count < total;
        var hasPrev = offset > 0;

        return new OffsetPage<RoomMemberModel>(items, offset, limit, total, hasPrev, hasNext);
    }

    public async Task<IReadOnlyList<RoomMemberModel>> ListRecentMembersAsync(
        Guid roomId,
        int limit,
        CancellationToken ct = default)
    {
        var sanitizedLimit = Math.Clamp(limit, 1, 50);

        var query = _context.RoomMembers
            .AsNoTracking()
            .Where(rm => rm.RoomId == roomId)
            .Join(
                _context.Users.AsNoTracking(),
                rm => rm.UserId,
                u => u.Id,
                (rm, u) => new
                {
                    rm.Role,
                    rm.Status,
                    rm.JoinedAt,
                    UserId = u.Id,
                    UserName = u.UserName ?? string.Empty,
                    u.FullName,
                    u.AvatarUrl,
                    u.Level
                })
            .OrderByDescending(x => x.JoinedAt)
            .ThenBy(x => x.UserId)
            .Take(sanitizedLimit)
            .Select(x => new RoomMemberModel(
                new MemberUserModel(x.UserId, x.UserName, x.FullName, x.AvatarUrl, x.Level),
                x.Role,
                x.Status,
                x.JoinedAt));

        return await query.ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<PagedResult<RoomDetailModel>> GetAllRoomsAsync(
        string? name,
        RoomJoinPolicy? joinPolicy,
        int? capacity,
        PageRequest paging,
        Guid? currentUserId,
        CancellationToken ct = default)
    {
        // Base query: all rooms (no club filter)
        IQueryable<Room> query = _context.Rooms
            .AsNoTracking()
            .Where(r => !r.IsDeleted);

        // Apply filters
        if (!string.IsNullOrWhiteSpace(name))
        {
            var normalizedName = name.Trim().ToUpperInvariant();
            query = query.Where(r => r.Name.ToUpper().Contains(normalizedName));
        }

        if (joinPolicy.HasValue)
        {
            query = query.Where(r => r.JoinPolicy == joinPolicy.Value);
        }

        if (capacity.HasValue)
        {
            query = query.Where(r => r.Capacity == capacity.Value);
        }

        // Sanitize paging
        var sanitized = new PageRequest(
            Page: paging.PageSafe,
            Size: Math.Clamp(paging.SizeSafe, 1, 50),
            Sort: string.IsNullOrWhiteSpace(paging.Sort) ? nameof(Room.CreatedAtUtc) : paging.Sort!,
            Desc: paging.Desc);

        var pagedRooms = await query
            .ToPagedResultAsync(sanitized, ct)
            .ConfigureAwait(false);

        if (pagedRooms.Items.Count == 0)
        {
            return new PagedResult<RoomDetailModel>(
                Array.Empty<RoomDetailModel>(),
                pagedRooms.Page,
                pagedRooms.Size,
                pagedRooms.TotalCount,
                pagedRooms.TotalPages,
                pagedRooms.HasPrevious,
                pagedRooms.HasNext,
                pagedRooms.Sort,
                pagedRooms.Desc);
        }

        var roomIds = pagedRooms.Items.Select(r => r.Id).ToArray();

        // Load owners for all rooms
        var owners = await _context.RoomMembers
            .AsNoTracking()
            .Where(rm => roomIds.Contains(rm.RoomId) && !rm.IsDeleted && rm.Role == RoomRole.Owner)
            .GroupBy(rm => rm.RoomId)
            .Select(g => new
            {
                RoomId = g.Key,
                OwnerId = g
                    .OrderBy(rm => rm.JoinedAt)
                    .Select(rm => rm.UserId)
                    .FirstOrDefault()
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var ownersMap = owners.ToDictionary(x => x.RoomId, x => x.OwnerId);

        Dictionary<Guid, (RoomMemberStatus Status, RoomRole Role)> membershipMap = new();

        if (currentUserId.HasValue)
        {
            var memberships = await _context.RoomMembers
                .AsNoTracking()
                .Where(rm => roomIds.Contains(rm.RoomId) && !rm.IsDeleted && rm.UserId == currentUserId.Value)
                .Select(rm => new
                {
                    rm.RoomId,
                    rm.Status,
                    rm.Role
                })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            membershipMap = memberships
                .ToDictionary(
                    x => x.RoomId,
                    x => (x.Status, x.Role));
        }

        var items = pagedRooms.Items
            .Select(r =>
            {
                var hasMember = membershipMap.TryGetValue(r.Id, out var memberInfo);

                var ownerId = ownersMap.TryGetValue(r.Id, out var foundOwner)
                    ? foundOwner
                    : Guid.Empty;

                var isMember = hasMember && memberInfo.Status == RoomMemberStatus.Approved;
                var isOwner = isMember && memberInfo.Role == RoomRole.Owner;
                var membershipStatus = hasMember ? memberInfo.Status : (RoomMemberStatus?)null;

                return new RoomDetailModel(
                    r.Id,
                    r.ClubId,
                    r.Name,
                    r.Description,
                    r.JoinPolicy,
                    r.Capacity,
                    r.MembersCount,
                    ownerId,
                    isMember,
                    isOwner,
                    membershipStatus,
                    r.CreatedAtUtc,
                    r.UpdatedAtUtc);
            })
            .ToList();

        return new PagedResult<RoomDetailModel>(
            items,
            pagedRooms.Page,
            pagedRooms.Size,
            pagedRooms.TotalCount,
            pagedRooms.TotalPages,
            pagedRooms.HasPrevious,
            pagedRooms.HasNext,
            pagedRooms.Sort,
            pagedRooms.Desc);
    }
}
