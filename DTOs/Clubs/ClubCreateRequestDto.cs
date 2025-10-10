namespace DTOs.Clubs;

/// <summary>
/// Request DTO for creating a new club.
/// Includes the community identifier where the club belongs.
/// </summary>
public sealed record ClubCreateRequestDto(
    Guid CommunityId,
    string Name,
    string? Description,
    bool IsPublic
);
