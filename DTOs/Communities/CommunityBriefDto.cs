namespace DTOs.Communities;

/// <summary>
/// Brief community information for search results.
/// </summary>
public sealed record CommunityBriefDto(
    Guid Id,
    string Name,
    string? School,
    bool IsPublic,
    int MembersCount
);
