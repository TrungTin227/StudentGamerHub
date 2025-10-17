namespace DTOs.Friends;

public sealed record FriendRequestItemDto(
    Guid UserId,
    string UserName,
    string FullName,
    string? AvatarUrl,
    string? University,
    string Status,
    DateTime RequestedAtUtc);
