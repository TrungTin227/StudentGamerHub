namespace DTOs.Clubs;

/// <summary>
/// Request DTO for updating club information.
/// </summary>
public sealed record ClubUpdateRequestDto(
    string Name,
    string? Description,
    bool IsPublic
);
