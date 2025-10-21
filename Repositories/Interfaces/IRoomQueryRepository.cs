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

}
