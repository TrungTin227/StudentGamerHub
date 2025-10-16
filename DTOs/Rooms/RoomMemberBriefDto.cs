namespace DTOs.Rooms;

/// <summary>
/// Brief information about a room member.
/// </summary>
public sealed record RoomMemberBriefDto(
    Guid UserId,
    string FullName,
    RoomRole Role,
    RoomMemberStatus Status,
    DateTime JoinedAt
);
