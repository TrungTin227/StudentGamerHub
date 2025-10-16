namespace Services.Interfaces;

/// <summary>
/// User-facing service for managing personal game library.
/// </summary>
public interface IUserGameService
{
    Task<Result> AddAsync(Guid currentUserId, Guid gameId, string? inGameName, GameSkillLevel? skill, CancellationToken ct = default);
    Task<Result> UpdateAsync(Guid currentUserId, Guid gameId, string? inGameName, GameSkillLevel? skill, CancellationToken ct = default);
    Task<Result> RemoveAsync(Guid currentUserId, Guid gameId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<UserGameDto>>> ListMineAsync(Guid currentUserId, CancellationToken ct = default);
}
