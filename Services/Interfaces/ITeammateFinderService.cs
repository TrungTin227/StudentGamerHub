namespace Services.Interfaces;

/// <summary>
/// Service for finding potential teammates based on game, university, skill level, and online status.
/// Enriches repository results with presence information and applies online-first sorting.
/// </summary>
public interface ITeammateFinderService
{
    /// <summary>
    /// Searches for potential teammates for the current user.
    /// Returns paginated results sorted by: online DESC ? points DESC ? sharedGames DESC ? userId DESC.
    /// </summary>
    /// <param name="currentUserId">Current user ID (excluded from results)</param>
    /// <param name="gameId">Optional: filter by specific game</param>
    /// <param name="university">Optional: filter by university</param>
    /// <param name="skill">Optional: filter by skill level</param>
    /// <param name="onlineOnly">If true, only return online users</param>
    /// <param name="paging">Page request for offset pagination</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing paginated TeammateDto list</returns>
    Task<Result<PagedResult<TeammateDto>>> SearchAsync(
        Guid currentUserId,
        Guid? gameId,
        string? university,
        GameSkillLevel? skill,
        bool onlineOnly,
        PageRequest paging,
        CancellationToken ct = default);
}
