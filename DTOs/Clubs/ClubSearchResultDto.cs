namespace DTOs.Clubs;

/// <summary>
/// Search result for clubs within a community with user's joined clubs info.
/// </summary>
public sealed record ClubSearchResultDto(
    IReadOnlyList<ClubBriefDto> Items,
    IReadOnlyList<Guid> JoinedClubIds,
    string? NextCursor,
    string? PrevCursor,
    int Size,
    string Sort,
    bool Desc
);
