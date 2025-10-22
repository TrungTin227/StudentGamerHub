namespace DTOs.Friends;

public sealed record UserBriefDto
{
    public required Guid Id { get; init; }

    public required string UserName { get; init; } = string.Empty;

    public string? FullName { get; init; }

    public string? AvatarUrl { get; init; }

    public int Level { get; init; }
}
