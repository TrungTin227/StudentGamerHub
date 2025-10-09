using DTOs.Friends;

namespace DTOs.Teammates;

/// <summary>
/// Represents a potential teammate candidate with online status and shared games count.
/// </summary>
public sealed record TeammateDto
{
    public required UserBriefDto User { get; init; }
    public bool IsOnline { get; init; }           // Set by service layer
    public int SharedGames { get; init; }         // For secondary sorting
}
