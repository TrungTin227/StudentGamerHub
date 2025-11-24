using BusinessObjects.Common.Pagination;
using DTOs.Common.Filters;
using Repositories.Models;

namespace Repositories.Interfaces;

/// <summary>
/// Query interface for Club entity - search and filtering
/// </summary>
public interface IClubQueryRepository
{
    /// <summary>
    /// Check if a club with the given name already exists in the community.
    /// </summary>
    /// <param name="communityId">Community ID</param>
    /// <param name="name">Club name to check (case-insensitive)</param>
    /// <param name="excludeId">Optional club ID to exclude from check (for updates)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if a club with this name exists in the community</returns>
    Task<bool> ExistsByNameInCommunityAsync(Guid communityId, string name, Guid? excludeId = null, CancellationToken ct = default);

    /// <summary>
    /// Search clubs within a community with filtering by name, visibility, and member count range.
    /// Uses cursor-based pagination with stable sorting by (MembersCount DESC, Id DESC).
    /// </summary>
    /// <param name="communityId">Community ID to search within</param>
    /// <param name="name">Filter by club name (case-insensitive, partial match)</param>
    /// <param name="isPublic">Filter by public/private status (null = all)</param>
    /// <param name="membersFrom">Minimum members count (inclusive)</param>
    /// <param name="membersTo">Maximum members count (inclusive)</param>
    /// <param name="cursor">Cursor pagination request</param>
    /// <param name="currentUserId">Current user ID for joining status</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of clubs and next cursor</returns>
    Task<(IReadOnlyList<ClubBriefModel> Items, string? NextCursor)> SearchClubsAsync(
        Guid communityId,
        string? name,
        bool? isPublic,
        int? membersFrom,
        int? membersTo,
        CursorRequest cursor,
        Guid? currentUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Get all clubs across all communities with filtering and pagination.
    /// Public endpoint accessible to any role.
    /// </summary>
    /// <param name="name">Filter by club name (case-insensitive, partial match)</param>
    /// <param name="isPublic">Filter by public/private status (null = all)</param>
    /// <param name="membersFrom">Minimum members count (inclusive)</param>
    /// <param name="membersTo">Maximum members count (inclusive)</param>
    /// <param name="paging">Page request for offset-based pagination</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paginated list of clubs</returns>
    Task<PagedResult<Club>> GetAllClubsAsync(
        string? name,
        bool? isPublic,
        int? membersFrom,
        int? membersTo,
        PageRequest paging,
        CancellationToken ct = default);

    /// <summary>
    /// Determine whether a community currently has any non-deleted clubs.
    /// </summary>
    Task<bool> AnyByCommunityAsync(Guid communityId, CancellationToken ct = default);

    /// <summary>
    /// Determine whether the club has any approved room members.
    /// </summary>
    /// <param name="clubId">Club ID</param>
    /// <param name="ct">Cancellation token</param>
    Task<bool> HasAnyApprovedRoomsAsync(Guid clubId, CancellationToken ct = default);

    /// <summary>
    /// Get club by ID.
    /// </summary>
    /// <param name="clubId">Club ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Club entity or null if not found</returns>
    Task<Club?> GetByIdAsync(Guid clubId, CancellationToken ct = default);

    /// <summary>
    /// Get membership record for a specific user in the club.
    /// </summary>
    Task<ClubMember?> GetMemberAsync(Guid clubId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Get projected detail model for API responses.
    /// </summary>
    Task<ClubDetailModel?> GetDetailsAsync(Guid clubId, Guid? currentUserId, CancellationToken ct = default);

    /// <summary>
    /// List club members with filtering and offset pagination.
    /// </summary>
    Task<OffsetPage<ClubMemberModel>> ListMembersAsync(
        Guid clubId,
        MemberListFilter filter,
        OffsetPaging paging,
        CancellationToken ct = default);

    /// <summary>
    /// Fetch the most recently joined club members limited by <paramref name="limit"/>.
    /// </summary>
    Task<IReadOnlyList<ClubMemberModel>> ListRecentMembersAsync(
        Guid clubId,
        int limit,
        CancellationToken ct = default);

    /// <summary>
    /// Count the number of clubs matching the specified criteria.
    /// </summary>
    /// <param name="communityId">Community ID to filter by</param>
    /// <param name="name">Optional name filter (case-insensitive)</param>
    /// <param name="isPublic">Optional visibility filter</param>
    /// <param name="membersFrom">Optional minimum members count</param>
    /// <param name="membersTo">Optional maximum members count</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The count of matching clubs</returns>
    Task<int> CountClubsAsync(
        Guid communityId,
        string? name,
        bool? isPublic,
        int? membersFrom,
        int? membersTo,
        CancellationToken ct = default);
}
