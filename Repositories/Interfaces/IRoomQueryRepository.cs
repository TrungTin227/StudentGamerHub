namespace Repositories.Interfaces;

/// <summary>
/// Query interface for Room entity - read operations
/// </summary>
public interface IRoomQueryRepository
{
    /// <summary>
    /// Get room with Club and Community navigation properties loaded.
    /// Uses joins to minimize queries.
    /// </summary>
    Task<Room?> GetRoomWithClubCommunityAsync(Guid roomId, CancellationToken ct = default);

    /// <summary>
    /// Get specific room member record.
    /// </summary>
    Task<RoomMember?> GetMemberAsync(Guid roomId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Count approved members in a room (Status == Approved).
    /// </summary>
    Task<int> CountApprovedMembersAsync(Guid roomId, CancellationToken ct = default);

    /// <summary>
    /// Check if user has any approved membership in any room of this club.
    /// </summary>
    Task<bool> HasAnyApprovedInClubAsync(Guid userId, Guid clubId, CancellationToken ct = default);

    /// <summary>
    /// Check if user has any approved membership in any room of any club in this community.
    /// </summary>
    Task<bool> HasAnyApprovedInCommunityAsync(Guid userId, Guid communityId, CancellationToken ct = default);
}
