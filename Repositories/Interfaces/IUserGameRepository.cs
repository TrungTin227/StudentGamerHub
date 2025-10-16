namespace Repositories.Interfaces;

/// <summary>
/// Repository for managing user-game relations.
/// </summary>
public interface IUserGameRepository
{
    /// <summary>
    /// Returns a queryable for user games (soft-delete filters applied).
    /// </summary>
    IQueryable<UserGame> Query();

    /// <summary>
    /// Gets a specific user-game relation by identifiers.
    /// </summary>
    Task<UserGame?> GetAsync(Guid userId, Guid gameId, CancellationToken ct = default);

    /// <summary>
    /// Lists all games for a specific user.
    /// </summary>
    Task<IReadOnlyList<UserGame>> ListByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new user-game relation.
    /// </summary>
    Task CreateAsync(UserGame userGame, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing relation.
    /// </summary>
    Task UpdateAsync(UserGame userGame, CancellationToken ct = default);

    /// <summary>
    /// Removes a relation.
    /// </summary>
    Task RemoveAsync(Guid userId, Guid gameId, CancellationToken ct = default);
}
