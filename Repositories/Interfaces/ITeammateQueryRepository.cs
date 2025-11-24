namespace Repositories.Interfaces;

/// <summary>
/// Query interface for teammate search functionality.
/// Repository layer returns minimal projection without online status (service layer handles that).
/// </summary>
public interface ITeammateQueryRepository
{
    /// <summary>
    /// Searches for potential teammates excluding the current user, 
    /// filtered by game, university, and skill level.
    /// Returns candidates with SharedGames count computed against current user's game list.
    /// Uses offset-based pagination with stable sorting by (Points DESC, SharedGames DESC, UserId DESC).
    /// </summary>
    /// <param name="currentUserId">Current user ID to exclude from results</param>
    /// <param name="filter">Search filters (game, university, skill)</param>
    /// <param name="paging">Page request for offset pagination</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paged result of candidates</returns>
    Task<PagedResult<TeammateCandidate>> SearchCandidatesAsync(
        Guid currentUserId,
        TeammateSearchFilter filter,
        PageRequest paging,
        CancellationToken ct = default);
}

/// <summary>
/// Filter criteria for teammate search.
/// All filters are optional (null means no filter applied).
/// </summary>
public sealed record TeammateSearchFilter(
    Guid? GameId,
    string? University,
    GameSkillLevel? Skill
);

/// <summary>
/// Minimal projection from database for a teammate candidate.
/// Does NOT include online status (that's computed by service layer).
/// </summary>
public sealed record TeammateCandidate(
    Guid UserId,
    string? FullName,
    string? AvatarUrl,
    string? University,
    int Points,
    int SharedGames  // Number of games shared with current user
);
