namespace DTOs.Friends;

public sealed record UserBriefDto(
    Guid Id,
    string UserName,
    string? AvatarUrl
);
