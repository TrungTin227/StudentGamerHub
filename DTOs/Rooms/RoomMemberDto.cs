using DTOs.Friends;

namespace DTOs.Rooms;

/// <summary>
/// Represents a member within a room directory listing.
/// </summary>
public sealed record RoomMemberDto
{
    public required UserBriefDto User { get; init; }

    public RoomRole Role { get; init; }

    public RoomMemberStatus Status { get; init; }

    public DateTime JoinedAtUtc { get; init; }

    public bool IsOwner { get; init; }

    public bool IsCurrentUser { get; init; }
}
