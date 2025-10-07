namespace DTOs.Friends;

public sealed record FriendRequestsDto(
    Guid Id,
    UserBriefDto Requester,
    UserBriefDto Addressee,
    DateTime RequestedAtUtc
);
