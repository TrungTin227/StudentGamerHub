namespace DTOs.Friends;

public sealed record SuggestedFriendDto(
    UserBriefDto User,
    int MutualFriendsCount
);
