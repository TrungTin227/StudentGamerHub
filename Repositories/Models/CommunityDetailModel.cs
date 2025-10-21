namespace Repositories.Models;

public sealed record CommunityDetailModel(
    Guid Id,
    string Name,
    string? Description,
    string? School,
    bool IsPublic,
    int MembersCount,
    int ClubsCount,
    Guid OwnerId,
    bool IsMember,
    bool IsOwner,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);
