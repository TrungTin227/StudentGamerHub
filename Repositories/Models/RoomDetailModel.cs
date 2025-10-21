using BusinessObjects.Common;

namespace Repositories.Models;

public sealed record RoomDetailModel(
    Guid Id,
    Guid ClubId,
    string Name,
    string? Description,
    RoomJoinPolicy JoinPolicy,
    int? Capacity,
    int MembersCount,
    Guid OwnerId,
    bool IsMember,
    bool IsOwner,
    RoomMemberStatus? MembershipStatus,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);
