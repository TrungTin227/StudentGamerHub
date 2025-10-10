namespace Services.Interfaces;

/// <summary>
/// Service for community search operations.
/// Provides cursor-based pagination for community discovery.
/// </summary>
public interface ICommunitySearchService
{
    /// <summary>
    /// Search communities with filtering and cursor-based pagination.
    /// Filters: school, game, visibility, member count range.
    /// Sorted by: MembersCount DESC, Id DESC (stable).
    /// </summary>
    /// <param name="school">Filter by school name (case-insensitive partial match)</param>
    /// <param name="gameId">Filter communities that include this game</param>
    /// <param name="isPublic">Filter by public/private status (null = all)</param>
    /// <param name="membersFrom">Minimum members count (inclusive)</param>
    /// <param name="membersTo">Maximum members count (inclusive)</param>
    /// <param name="cursor">Cursor pagination request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paginated list of community briefs</returns>
    Task<Result<CursorPageResult<CommunityBriefDto>>> SearchAsync(
        string? school, 
        Guid? gameId, 
        bool? isPublic, 
        int? membersFrom, 
        int? membersTo,
        CursorRequest cursor, 
        CancellationToken ct = default);
}
