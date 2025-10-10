namespace DTOs.Rooms;

/// <summary>
/// Request DTO for creating a new room.
/// </summary>
public sealed record RoomCreateRequestDto(
    Guid ClubId,
    string Name,
    string? Description,
    RoomJoinPolicy JoinPolicy,
    string? Password,
    int? Capacity
);
