using Repositories.Models;

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
    /// Get a room by ID with Club and Community navigations tracked for updates.
    /// </summary>
    Task<Room?> GetByIdAsync(Guid roomId, CancellationToken ct = default);

    /// <summary>
    /// Get specific room member record.
    /// </summary>
    Task<RoomMember?> GetMemberAsync(Guid roomId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Projected room detail for API responses.
    /// </summary>
    Task<RoomDetailModel?> GetDetailsAsync(Guid roomId, Guid? currentUserId, CancellationToken ct = default);

    /// <summary>
    /// Count approved members in a room (Status == Approved).
    /// </summary>
    Task<int> CountApprovedMembersAsync(Guid roomId, CancellationToken ct = default);

    /// <summary>
    /// List room members with pagination support.
    /// </summary>
    Task<IReadOnlyList<RoomMember>> ListMembersAsync(Guid roomId, int take, int skip, CancellationToken ct = default);

    /// <summary>
    /// Check if user has any approved membership in any room of this club.
    /// </summary>
    Task<bool> HasAnyApprovedInClubAsync(Guid userId, Guid clubId, CancellationToken ct = default);

    /// <summary>
    /// Check if user has any approved membership in any room of any club in this community.
    /// </summary>
    Task<bool> HasAnyApprovedInCommunityAsync(Guid userId, Guid communityId, CancellationToken ct = default);

    /// <summary>
    /// List room memberships for a user within a club.
    /// </summary>
    Task<IReadOnlyList<RoomMember>> ListMembershipsAsync(Guid clubId, Guid userId, CancellationToken ct = default);

}
