namespace Services.Interfaces;

/// <summary>
/// Service for room membership operations.
/// </summary>
public interface IRoomService
{
    Task<Result<RoomDetailDto>> CreateRoomAsync(RoomCreateRequestDto req, Guid currentUserId, CancellationToken ct = default);
    Task<Result<RoomDetailDto>> JoinRoomAsync(Guid roomId, Guid currentUserId, RoomJoinRequestDto? req, CancellationToken ct = default);
    Task<Result> KickRoomMemberAsync(Guid roomId, Guid targetUserId, Guid actorUserId, CancellationToken ct = default);
    Task<Result<RoomDetailDto>> GetByIdAsync(Guid roomId, Guid? currentUserId = null, CancellationToken ct = default);
}
