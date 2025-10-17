namespace DTOs.Games;

/// <summary>
/// Lightweight game catalog item.
/// </summary>
public sealed record GameDto(
    Guid Id,
    string Name);
public sealed record GameBriefDto(
    Guid Id,
    string Name,
    string? InGameName,
    DateTime AddedAt,
    GameSkillLevel? Skill
);