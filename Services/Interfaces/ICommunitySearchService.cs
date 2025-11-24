namespace Services.Interfaces;

/// <summary>
/// Service for community search operations.
/// </summary>
public interface ICommunitySearchService
{
    /// <summary>
    /// Search communities with filtering and offset-based pagination.
    /// Filters: school, game, visibility, member count range.
    /// Sorted by: MembersCount DESC, Id DESC (stable).
    /// </summary>
    /// <param name="school">Filter by school name (case-insensitive partial match)</param>
    /// <param name="gameId">Filter communities that include this game</param>
    /// <param name="isPublic">Filter by public/private status (null = all)</param>
    /// <param name="membersFrom">Minimum members count (inclusive)</param>
    /// <param name="membersTo">Maximum members count (inclusive)</param>
    /// <param name="paging">Page request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paginated list of community briefs with page info</returns>
    Task<Result<PagedResult<CommunityBriefDto>>> SearchAsync(
        string? school, 
        Guid? gameId, 
        bool? isPublic, 
        int? membersFrom, 
        int? membersTo,
        PageRequest paging, 
        CancellationToken ct = default);
}
