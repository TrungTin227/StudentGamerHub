using BusinessObjects.Common.Pagination;
using Repositories.Models;

namespace Repositories.Interfaces;

/// <summary>
/// Query interface for Community entity - search and filtering
/// </summary>
public interface ICommunityQueryRepository
{
    /// <summary>
    /// Get a community by identifier.
    /// </summary>
    Task<Community?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Get current membership record, if any.
    /// </summary>
    Task<CommunityMember?> GetMemberAsync(Guid communityId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Projected details for API responses.
    /// </summary>
    Task<CommunityDetailModel?> GetDetailsAsync(Guid communityId, Guid? currentUserId, CancellationToken ct = default);

    /// <summary>
    /// Determine whether the community still has any approved room members.
    /// </summary>
    Task<bool> HasAnyApprovedRoomsAsync(Guid communityId, CancellationToken ct = default);

    /// <summary>
    /// Search communities with filtering by school, game, visibility, and member count range.
    /// Uses cursor-based pagination with stable sorting by (MembersCount DESC, Id DESC).
    /// </summary>
    /// <param name="school">Filter by school name (case-insensitive, partial match)</param>
    /// <param name="gameId">Filter communities that include this game</param>
    /// <param name="isPublic">Filter by public/private status (null = all)</param>
    /// <param name="membersFrom">Minimum members count (inclusive)</param>
    /// <param name="membersTo">Maximum members count (inclusive)</param>
    /// <param name="cursor">Cursor pagination request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of communities and next cursor</returns>
    Task<(IReadOnlyList<Community> Items, string? NextCursor)> SearchCommunitiesAsync(
        string? school,
        Guid? gameId,
        bool? isPublic,
        int? membersFrom,
        int? membersTo,
        CursorRequest cursor,
        CancellationToken ct = default);

    /// <summary>
    /// Discover communities using offset pagination with optional free-text filtering.
    /// </summary>
    Task<PagedResult<CommunityDetailModel>> SearchDiscoverAsync(
        Guid? currentUserId,
        string? query,
        bool orderByTrending,
        PageRequest paging,
        CancellationToken ct = default);
}
