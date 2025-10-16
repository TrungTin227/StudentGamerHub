namespace Services.Interfaces;

/// <summary>
/// Admin-facing catalog service for games.
/// </summary>
public interface IGameCatalogService
{
    Task<Result<Guid>> CreateAsync(string name, CancellationToken ct = default);
    Task<Result> RenameAsync(Guid gameId, string newName, CancellationToken ct = default);
    Task<Result> SoftDeleteAsync(Guid gameId, CancellationToken ct = default);
    Task<Result<CursorPageResult<GameDto>>> SearchAsync(string? query, CursorRequest cursor, CancellationToken ct = default);
}
