using Microsoft.EntityFrameworkCore;
using Repositories.Models;

namespace Repositories.Implements;

/// <summary>
/// Room command implementation for write operations.
/// Does NOT manage transactions - caller must use ExecuteTransactionAsync.
/// Updates counters for Room, Club, and Community MembersCount.
/// </summary>
public sealed class RoomCommandRepository : IRoomCommandRepository
{
    private readonly AppDbContext _context;

    public RoomCommandRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Create a new room.
    /// </summary>
    public async Task CreateRoomAsync(Room room, CancellationToken ct = default)
    {
        await _context.Rooms.AddAsync(room, ct);
    }

    /// <summary>
    /// Update existing room information.
    /// </summary>
    public Task UpdateRoomAsync(Room room, CancellationToken ct = default)
    {
        _context.Rooms.Update(room);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Soft delete a room by updating soft-delete audit fields.
    /// </summary>
    public async Task SoftDeleteRoomAsync(Guid roomId, Guid deletedBy, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        await _context.Rooms
            .IgnoreQueryFilters()
            .Where(r => r.Id == roomId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.IsDeleted, r => true)
                .SetProperty(r => r.DeletedAtUtc, r => now)
                .SetProperty(r => r.DeletedBy, r => deletedBy)
                .SetProperty(r => r.UpdatedAtUtc, r => now)
                .SetProperty(r => r.UpdatedBy, r => deletedBy)
                .SetProperty(r => r.MembersCount, r => 0), ct);
    }

    /// <summary>
    /// Upsert room member (insert or update).
    /// </summary>
    public async Task UpsertMemberAsync(RoomMember member, CancellationToken ct = default)
    {
        var updated = await _context.RoomMembers
            .Where(rm => rm.RoomId == member.RoomId && rm.UserId == member.UserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(rm => rm.Role, _ => member.Role)
                .SetProperty(rm => rm.Status, _ => member.Status)
                .SetProperty(rm => rm.JoinedAt, _ => member.JoinedAt), ct)
            .ConfigureAwait(false);

        if (updated == 0)
        {
            await _context.RoomMembers.AddAsync(member, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Update existing room member.
    /// </summary>
    public Task UpdateMemberAsync(RoomMember member, CancellationToken ct = default)
    {
        _context.RoomMembers.Update(member);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Remove member from room.
    /// </summary>
    public async Task<RoomMemberStatus?> RemoveMemberAsync(Guid roomId, Guid userId, CancellationToken ct = default)
    {
        var status = await _context.RoomMembers
            .Where(rm => rm.RoomId == roomId && rm.UserId == userId)
            .Select(rm => (RoomMemberStatus?)rm.Status)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (status is null)
        {
            return null;
        }

        await _context.RoomMembers
            .Where(rm => rm.RoomId == roomId && rm.UserId == userId)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        return status;
    }

    public void Detach(RoomMember member)
    {
        if (member is null)
        {
            return;
        }

        var entry = _context.Entry(member);
        if (entry is not null)
        {
            entry.State = EntityState.Detached;
        }
    }

    public async Task<RoomMemberStatus?> GetMemberStatusAsync(Guid roomId, Guid userId, CancellationToken ct = default)
    {
        return await _context.RoomMembers
            .Where(rm => rm.RoomId == roomId && rm.UserId == userId)
            .Select(rm => (RoomMemberStatus?)rm.Status)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task UpdateMemberStatusAsync(Guid roomId, Guid userId, RoomMemberStatus status, DateTime joinedAt, Guid updatedBy, CancellationToken ct = default)
    {
        await _context.RoomMembers
            .Where(rm => rm.RoomId == roomId && rm.UserId == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(rm => rm.Status, _ => status)
                .SetProperty(rm => rm.JoinedAt, _ => joinedAt)
                .SetProperty(rm => rm.UpdatedAtUtc, _ => DateTime.UtcNow)
                .SetProperty(rm => rm.UpdatedBy, _ => updatedBy), ct)
            .ConfigureAwait(false);
    }

    public async Task<int> CountApprovedMembersAsync(Guid roomId, CancellationToken ct = default)
    {
        return await _context.RoomMembers
            .Where(rm => rm.RoomId == roomId && rm.Status == RoomMemberStatus.Approved)
            .CountAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RoomMembershipRemovalSummary>> RemoveMembershipsByClubAsync(Guid clubId, Guid userId, CancellationToken ct = default)
    {
        var summaries = await _context.RoomMembers
            .Where(rm => rm.UserId == userId && rm.Room!.ClubId == clubId)
            .GroupBy(rm => rm.RoomId)
            .Select(g => new RoomMembershipRemovalSummary(
                g.Key,
                g.Count(rm => rm.Status == RoomMemberStatus.Approved)))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (summaries.Count == 0)
        {
            return Array.Empty<RoomMembershipRemovalSummary>();
        }

        await _context.RoomMembers
            .Where(rm => rm.UserId == userId && rm.Room!.ClubId == clubId)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        return summaries;
    }

    public async Task<IReadOnlyList<RoomMembershipRemovalSummary>> RemoveMembershipsByCommunityAsync(Guid communityId, Guid userId, CancellationToken ct = default)
    {
        var summaries = await _context.RoomMembers
            .Where(rm => rm.UserId == userId && rm.Room!.Club!.CommunityId == communityId)
            .GroupBy(rm => rm.RoomId)
            .Select(g => new RoomMembershipRemovalSummary(
                g.Key,
                g.Count(rm => rm.Status == RoomMemberStatus.Approved)))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (summaries.Count == 0)
        {
            return Array.Empty<RoomMembershipRemovalSummary>();
        }

        await _context.RoomMembers
            .Where(rm => rm.UserId == userId && rm.Room!.Club!.CommunityId == communityId)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        return summaries;
    }

    /// <summary>
    /// Increment/decrement room members count.
    /// Uses ExecuteUpdateAsync for atomic operation with guard against negative values.
    /// </summary>
    public async Task IncrementRoomMembersAsync(Guid roomId, int delta, CancellationToken ct = default)
    {
        await _context.Rooms
            .Where(r => r.Id == roomId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.MembersCount, r => EF.Functions.Greatest(r.MembersCount + delta, 0)), ct);
    }

    /// <summary>
    /// Increment/decrement club members count.
    /// Uses ExecuteUpdateAsync for atomic operation with guard against negative values.
    /// </summary>
    public async Task IncrementClubMembersAsync(Guid clubId, int delta, CancellationToken ct = default)
    {
        await _context.Clubs
            .Where(c => c.Id == clubId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(c => c.MembersCount, c => EF.Functions.Greatest(c.MembersCount + delta, 0)), ct);
    }

    /// <summary>
    /// Increment/decrement community members count.
    /// Uses ExecuteUpdateAsync for atomic operation with guard against negative values.
    /// </summary>
    public async Task IncrementCommunityMembersAsync(Guid communityId, int delta, CancellationToken ct = default)
    {
        await _context.Communities
            .Where(c => c.Id == communityId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(c => c.MembersCount, c => EF.Functions.Greatest(c.MembersCount + delta, 0)), ct);
    }
}
