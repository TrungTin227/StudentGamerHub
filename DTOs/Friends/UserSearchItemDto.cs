namespace DTOs.Friends;

public sealed record UserSearchItemDto(
    Guid UserId,
    string UserName,
    string FullName,
    string? AvatarUrl,
    string? University,
    bool IsFriend,
    bool IsPending);
