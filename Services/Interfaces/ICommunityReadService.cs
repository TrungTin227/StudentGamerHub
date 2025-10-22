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
}
