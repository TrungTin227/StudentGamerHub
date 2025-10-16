using BusinessObjects.Common;

namespace DTOs.Games;

/// <summary>
/// Represents a user's association with a game.
/// </summary>
public sealed record UserGameDto(
    Guid GameId,
    string GameName,
    string? InGameName,
    GameSkillLevel? Skill,
    DateTimeOffset AddedAt);
