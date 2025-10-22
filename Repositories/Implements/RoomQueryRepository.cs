using BusinessObjects.Common.Pagination;
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
        var sort = string.IsNullOrWhiteSpace(paging.Sort) ? nameof(RoomDetailModel.CreatedAtUtc) : paging.Sort!;
        var sanitized = new PageRequest(
            Page: paging.PageSafe,
            Size: Math.Clamp(paging.SizeSafe, 1, 50),
            Sort: sort,
            Desc: paging.Desc);

        var query = _context.Rooms
            .AsNoTracking()
            .Where(r => r.ClubId == clubId)
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

        return await query.ToPagedResultAsync(sanitized, ct).ConfigureAwait(false);
    }

    public async Task<bool> AnyByClubAsync(Guid clubId, CancellationToken ct = default)
    {
        return await _context.Rooms
            .AnyAsync(r => r.ClubId == clubId, ct)
            .ConfigureAwait(false);
    }

}
