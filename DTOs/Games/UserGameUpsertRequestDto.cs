using BusinessObjects.Common;

namespace DTOs.Games;

/// <summary>
/// Request payload for creating or updating a user-game relation.
/// </summary>
public sealed record UserGameUpsertRequestDto(
    string? InGameName,
    GameSkillLevel? Skill);
