using BusinessObjects.Common.Pagination;
using DTOs.Common.Filters;
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
    /// List rooms within a club with pagination and membership flags.
    /// </summary>
    Task<PagedResult<RoomDetailModel>> ListByClubAsync(Guid clubId, Guid? currentUserId, PageRequest paging, CancellationToken ct = default);

    /// <summary>
    /// Determine if a club currently has any active rooms (respecting soft-delete filters).
    /// </summary>
    Task<bool> AnyByClubAsync(Guid clubId, CancellationToken ct = default);

    /// <summary>
    /// List room members with filtering and offset pagination.
    /// </summary>
    Task<OffsetPage<RoomMemberModel>> ListMembersAsync(
        Guid roomId,
        RoomMemberListFilter filter,
        OffsetPaging paging,
        CancellationToken ct = default);

    /// <summary>
    /// Fetch the most recently joined room members limited by <paramref name="limit"/>.
    /// </summary>
    Task<IReadOnlyList<RoomMemberModel>> ListRecentMembersAsync(
        Guid roomId,
        int limit,
        CancellationToken ct = default);

}
