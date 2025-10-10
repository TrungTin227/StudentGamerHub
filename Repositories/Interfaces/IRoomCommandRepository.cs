namespace Repositories.Interfaces;

/// <summary>
/// Command interface for Room entity - write operations
/// Does NOT manage transactions - caller must use ExecuteTransactionAsync
/// </summary>
public interface IRoomCommandRepository
{
    /// <summary>
    /// Create a new room.
    /// </summary>
    Task CreateRoomAsync(Room room, CancellationToken ct = default);

    /// <summary>
    /// Update existing room information.
    /// </summary>
    Task UpdateRoomAsync(Room room, CancellationToken ct = default);

    /// <summary>
    /// Soft delete a room by ID.
    /// </summary>
    Task SoftDeleteRoomAsync(Guid roomId, Guid deletedBy, CancellationToken ct = default);

    /// <summary>
    /// Upsert room member (insert or update if exists).
    /// </summary>
    Task UpsertMemberAsync(RoomMember member, CancellationToken ct = default);

    /// <summary>
    /// Update existing room member.
    /// </summary>
    Task UpdateMemberAsync(RoomMember member, CancellationToken ct = default);

    /// <summary>
    /// Remove member from room.
    /// </summary>
    Task RemoveMemberAsync(Guid roomId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Increment/decrement room members count.
    /// Delta can be positive or negative.
    /// </summary>
    Task IncrementRoomMembersAsync(Guid roomId, int delta, CancellationToken ct = default);

    /// <summary>
    /// Increment/decrement club members count.
    /// Delta can be positive or negative.
    /// </summary>
    Task IncrementClubMembersAsync(Guid clubId, int delta, CancellationToken ct = default);

    /// <summary>
    /// Increment/decrement community members count.
    /// Delta can be positive or negative.
    /// </summary>
    Task IncrementCommunityMembersAsync(Guid communityId, int delta, CancellationToken ct = default);
}
