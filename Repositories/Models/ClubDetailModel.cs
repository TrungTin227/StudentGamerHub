namespace Repositories.Models;

public sealed record ClubDetailModel(
    Guid Id,
    Guid CommunityId,
    string Name,
    string? Description,
    bool IsPublic,
    int MembersCount,
    int RoomsCount,
    Guid OwnerId,
    bool IsMember,
    bool IsCommunityMember,
    bool IsOwner,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);
