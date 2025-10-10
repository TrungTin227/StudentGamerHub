namespace DTOs.Rooms;

/// <summary>
/// Request DTO for updating a room.
/// </summary>
public sealed record RoomUpdateRequestDto(
    string Name,
    string? Description,
    RoomJoinPolicy JoinPolicy,
    string? Password,
    int? Capacity
);
