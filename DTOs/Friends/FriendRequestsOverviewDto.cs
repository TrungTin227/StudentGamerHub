namespace DTOs.Friends;

public sealed record FriendRequestsOverviewDto(
    IReadOnlyList<FriendRequestItemDto> Incoming,
    IReadOnlyList<FriendRequestItemDto> Outgoing);
