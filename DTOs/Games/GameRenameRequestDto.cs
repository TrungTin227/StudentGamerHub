namespace DTOs.Games;

/// <summary>
/// Request payload for renaming an existing game.
/// </summary>
public sealed record GameRenameRequestDto(string Name);
