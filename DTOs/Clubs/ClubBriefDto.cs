namespace DTOs.Clubs;

/// <summary>
/// Brief club information for search results.
/// </summary>
public sealed record ClubBriefDto(
    Guid Id,
    Guid CommunityId,
    string Name,
    bool IsPublic,
    int MembersCount,
    string? Description
);
