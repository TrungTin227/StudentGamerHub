using Repositories.Models;

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
    /// Remove member from room and return previous status if present.
    /// </summary>
    Task<RoomMemberStatus?> RemoveMemberAsync(Guid roomId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Detach a tracked room member instance (used after failed inserts).
    /// </summary>
    void Detach(RoomMember member);

    /// <summary>
    /// Get current member status for capacity validations.
    /// </summary>
    Task<RoomMemberStatus?> GetMemberStatusAsync(Guid roomId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Update member status and joined timestamp.
    /// </summary>
    Task UpdateMemberStatusAsync(Guid roomId, Guid userId, RoomMemberStatus status, DateTime joinedAt, Guid updatedBy, CancellationToken ct = default);

    /// <summary>
    /// Count approved members in a room.
    /// </summary>
    Task<int> CountApprovedMembersAsync(Guid roomId, CancellationToken ct = default);

    /// <summary>
    /// Remove all room memberships of a user in a club and return removed approved counts per room.
    /// </summary>
    Task<IReadOnlyList<RoomMembershipRemovalSummary>> RemoveMembershipsByClubAsync(Guid clubId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Remove all room memberships of a user in a community and return removed approved counts per room.
    /// </summary>
    Task<IReadOnlyList<RoomMembershipRemovalSummary>> RemoveMembershipsByCommunityAsync(Guid communityId, Guid userId, CancellationToken ct = default);

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
