namespace Services.Interfaces;

/// <summary>
/// Read operations for community discovery and browsing.
/// </summary>
public interface ICommunityReadService
{
    /// <summary>
    /// Discover communities with optional free-text filtering and ordering.
    /// </summary>
    Task<Result<PagedResult<CommunityDetailDto>>> SearchDiscoverAsync(
        Guid? currentUserId,
        string? query,
        string orderBy,
        OffsetPaging paging,
        CancellationToken ct = default);

    /// <summary>
    /// List members of a community using offset pagination and optional filtering.
    /// </summary>
    Task<Result<OffsetPage<CommunityMemberDto>>> ListMembersAsync(
        Guid communityId,
        MemberListFilter filter,
        OffsetPaging paging,
        Guid? currentUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Get the most recently joined members of a community.
    /// </summary>
    Task<Result<IReadOnlyList<CommunityMemberDto>>> ListRecentMembersAsync(
        Guid communityId,
        int limit,
        Guid? currentUserId,
        CancellationToken ct = default);
}
