namespace Repositories.Models;

public sealed record ClubBriefModel(
    Guid Id,
    Guid CommunityId,
    string Name,
    string? Description,
    bool IsPublic,
    int MembersCount,
    bool IsJoined
);
