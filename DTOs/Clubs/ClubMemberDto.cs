using DTOs.Friends;

namespace DTOs.Clubs;

/// <summary>
/// Represents a club member entry in directory listings.
/// </summary>
public sealed record ClubMemberDto
{
    public required UserBriefDto User { get; init; }

    public MemberRole Role { get; init; }

    public DateTime JoinedAtUtc { get; init; }

    public bool IsOwner { get; init; }

    public bool IsCurrentUser { get; init; }
}
