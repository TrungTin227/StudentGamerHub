namespace Repositories.Interfaces;

/// <summary>
/// Repository for querying and persisting <see cref="Game"/> entities.
/// </summary>
public interface IGameRepository
{
    /// <summary>
    /// Returns a queryable for games (soft-delete filter applied automatically).
    /// </summary>
    IQueryable<Game> Query();

    /// <summary>
    /// Finds a game by identifier.
    /// </summary>
    Task<Game?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Finds a game by name ignoring casing (soft-deleted records are skipped).
    /// </summary>
    Task<Game?> GetByNameInsensitiveAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Persists a new game.
    /// </summary>
    Task CreateAsync(Game game, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing game.
    /// </summary>
    Task UpdateAsync(Game game, CancellationToken ct = default);

    /// <summary>
    /// Applies soft-delete changes to an existing game.
    /// </summary>
    Task SoftDeleteAsync(Game game, CancellationToken ct = default);
}
