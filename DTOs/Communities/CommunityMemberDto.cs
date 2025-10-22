using DTOs.Friends;

namespace DTOs.Communities;

/// <summary>
/// Represents a member within a community directory listing.
/// </summary>
public sealed record CommunityMemberDto
{
    public required UserBriefDto User { get; init; }

    public MemberRole Role { get; init; }

    public DateTime JoinedAtUtc { get; init; }

    public bool IsOwner { get; init; }

    public bool IsCurrentUser { get; init; }
}
