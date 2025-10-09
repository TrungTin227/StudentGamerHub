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
    /// Uses cursor-based pagination with stable sorting by (Points DESC, SharedGames DESC, UserId DESC).
    /// </summary>
    /// <param name="currentUserId">Current user ID to exclude from results</param>
    /// <param name="filter">Search filters (game, university, skill)</param>
    /// <param name="cursor">Cursor pagination request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of candidates and next cursor</returns>
    Task<(IReadOnlyList<TeammateCandidate> Candidates, string? NextCursor)>
        SearchCandidatesAsync(
            Guid currentUserId,
            TeammateSearchFilter filter,
            CursorRequest cursor,
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
