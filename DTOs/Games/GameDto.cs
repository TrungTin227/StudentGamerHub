namespace DTOs.Games;

/// <summary>
/// Lightweight game catalog item.
/// </summary>
public sealed record GameDto(
    Guid Id,
    string Name);
