using Microsoft.EntityFrameworkCore;

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

    /// <summary>
    /// Count approved members in room.
    /// Uses Status index for performance.
    /// </summary>
    public async Task<int> CountApprovedMembersAsync(Guid roomId, CancellationToken ct = default)
    {
        return await _context.RoomMembers
            .AsNoTracking()
            .Where(rm => rm.RoomId == roomId && rm.Status == RoomMemberStatus.Approved)
            .CountAsync(ct);
    }

    /// <summary>
    /// List members in a room with pagination.
    /// </summary>
    public async Task<IReadOnlyList<RoomMember>> ListMembersAsync(Guid roomId, int take, int skip, CancellationToken ct = default)
    {
        return await _context.RoomMembers
            .AsNoTracking()
            .Where(rm => rm.RoomId == roomId)
            .Include(rm => rm.User)
            .OrderBy(rm => rm.JoinedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Check if user has any approved membership in any room of this club.
    /// Joins Room->Club to filter by clubId, then checks approved status.
    /// </summary>
    public async Task<bool> HasAnyApprovedInClubAsync(Guid userId, Guid clubId, CancellationToken ct = default)
    {
        return await _context.RoomMembers
            .AsNoTracking()
            .Where(rm => rm.UserId == userId
                      && rm.Status == RoomMemberStatus.Approved
                      && rm.Room!.ClubId == clubId)
            .AnyAsync(ct);
    }

    /// <summary>
    /// Check if user has any approved membership in any room of any club in this community.
    /// Joins Room->Club->Community to filter by communityId.
    /// </summary>
    public async Task<bool> HasAnyApprovedInCommunityAsync(Guid userId, Guid communityId, CancellationToken ct = default)
    {
        return await _context.RoomMembers
            .AsNoTracking()
            .Where(rm => rm.UserId == userId
                      && rm.Status == RoomMemberStatus.Approved
                      && rm.Room!.Club!.CommunityId == communityId)
            .AnyAsync(ct);
    }
}
