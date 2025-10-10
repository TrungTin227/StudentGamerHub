using Microsoft.EntityFrameworkCore;

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
        var existing = await _context.RoomMembers
            .FirstOrDefaultAsync(rm => rm.RoomId == member.RoomId && rm.UserId == member.UserId, ct);

        if (existing is null)
        {
            await _context.RoomMembers.AddAsync(member, ct);
        }
        else
        {
            // Update properties
            existing.Role = member.Role;
            existing.Status = member.Status;
            existing.JoinedAt = member.JoinedAt;
            _context.RoomMembers.Update(existing);
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
    public async Task RemoveMemberAsync(Guid roomId, Guid userId, CancellationToken ct = default)
    {
        var member = await _context.RoomMembers
            .FirstOrDefaultAsync(rm => rm.RoomId == roomId && rm.UserId == userId, ct);

        if (member is not null)
        {
            _context.RoomMembers.Remove(member);
        }
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
