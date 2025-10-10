namespace DTOs.Rooms;

/// <summary>
/// Detailed information about a room.
/// </summary>
public sealed record RoomDetailDto(
    Guid Id,
    Guid ClubId,
    string Name,
    string? Description,
    RoomJoinPolicy JoinPolicy,
    int? Capacity,
    int MembersCount
);
