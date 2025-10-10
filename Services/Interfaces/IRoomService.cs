namespace Services.Interfaces;

/// <summary>
/// Service for room management operations.
/// Handles room creation, joining, member approval, leaving, and moderation.
/// </summary>
public interface IRoomService
{
    /// <summary>
    /// Create a new room within a club.
    /// Owner is automatically approved and gets Owner role.
    /// </summary>
    /// <param name="currentUserId">User creating the room (becomes Owner)</param>
    /// <param name="clubId">Club ID where room will be created</param>
    /// <param name="name">Room name</param>
    /// <param name="description">Room description (optional)</param>
    /// <param name="policy">Join policy (Open, RequiresApproval, RequiresPassword)</param>
    /// <param name="password">Password (required if policy = RequiresPassword)</param>
    /// <param name="capacity">Maximum member capacity (optional)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Created room ID</returns>
    Task<Result<Guid>> CreateRoomAsync(
        Guid currentUserId, 
        Guid clubId, 
        string name, 
        string? description,
        RoomJoinPolicy policy, 
        string? password, 
        int? capacity, 
        CancellationToken ct = default);

    /// <summary>
    /// Join a room based on its policy.
    /// - Open: Approved immediately (if capacity allows)
    /// - RequiresApproval: Set to Pending status
    /// - RequiresPassword: Verify password, then approve if valid
    /// </summary>
    /// <param name="currentUserId">User attempting to join</param>
    /// <param name="roomId">Room ID to join</param>
    /// <param name="password">Password (required for RequiresPassword policy)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success or error</returns>
    Task<Result> JoinRoomAsync(
        Guid currentUserId, 
        Guid roomId, 
        string? password, 
        CancellationToken ct = default);

    /// <summary>
    /// Approve a pending member (Owner/Moderator only).
    /// Changes status from Pending to Approved and updates counters.
    /// </summary>
    /// <param name="currentUserId">User approving (must be Owner/Moderator)</param>
    /// <param name="roomId">Room ID</param>
    /// <param name="targetUserId">User to approve</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success or error</returns>
    Task<Result> ApproveMemberAsync(
        Guid currentUserId, 
        Guid roomId, 
        Guid targetUserId, 
        CancellationToken ct = default);

    /// <summary>
    /// Leave a room.
    /// Owner cannot leave (Forbidden in MVP).
    /// Approved members: updates counters; Pending members: just removed.
    /// </summary>
    /// <param name="currentUserId">User leaving</param>
    /// <param name="roomId">Room ID to leave</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success or error</returns>
    Task<Result> LeaveRoomAsync(
        Guid currentUserId, 
        Guid roomId, 
        CancellationToken ct = default);

    /// <summary>
    /// Kick or ban a member (Owner/Moderator only).
    /// Approved members: status changed to Banned/Rejected, counters updated.
    /// Pending members: status changed to Banned/Rejected, no counter updates.
    /// </summary>
    /// <param name="currentUserId">User performing action (must be Owner/Moderator)</param>
    /// <param name="roomId">Room ID</param>
    /// <param name="targetUserId">User to kick/ban</param>
    /// <param name="ban">True to ban, false to just kick</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Success or error</returns>
    Task<Result> KickOrBanAsync(
        Guid currentUserId,
        Guid roomId,
        Guid targetUserId,
        bool ban,
        CancellationToken ct = default);

    /// <summary>
    /// Get room detail by ID.
    /// </summary>
    Task<Result<RoomDetailDto>> GetByIdAsync(Guid roomId, CancellationToken ct = default);

    /// <summary>
    /// List room members with pagination.
    /// </summary>
    Task<Result<IReadOnlyList<RoomMemberBriefDto>>> ListMembersAsync(Guid roomId, int skip, int take, CancellationToken ct = default);

    /// <summary>
    /// Update room metadata. Only owner can update.
    /// </summary>
    Task<Result> UpdateRoomAsync(Guid currentUserId, Guid roomId, RoomUpdateRequestDto req, CancellationToken ct = default);

    /// <summary>
    /// Transfer ownership to another approved member.
    /// </summary>
    Task<Result> TransferOwnershipAsync(Guid currentUserId, Guid roomId, Guid newOwnerUserId, CancellationToken ct = default);

    /// <summary>
    /// Archive a room (soft delete). Only allowed when no other approved members remain.
    /// </summary>
    Task<Result> ArchiveRoomAsync(Guid currentUserId, Guid roomId, CancellationToken ct = default);
}
