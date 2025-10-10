namespace DTOs.Clubs;

/// <summary>
/// Request DTO for creating a new club.
/// CommunityId is provided in the route parameter.
/// </summary>
public sealed record ClubCreateRequestDto(
    string Name,
    string? Description,
    bool IsPublic
);
