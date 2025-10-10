namespace DTOs.Rooms;

/// <summary>
/// Request DTO for joining a room.
/// </summary>
public sealed record RoomJoinRequestDto(
    string? Password
);
