namespace DTOs.Friends;

public sealed record FriendDto(
    Guid Id,
    UserBriefDto User,
    DateTime? BecameFriendsAtUtc
);
