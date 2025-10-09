namespace Services.Application.Quests;

/// <summary>
/// Daily Quests MVP: 4 quests (CheckInDaily, JoinAnyRoom, InviteAccepted, AttendEvent).
/// - Redis l?u flag ho�n th�nh per-user per-day (TTL ??n 00:00 VN h�m sau).
/// - Idempotent: 1 quest ch? complete 1 l?n/ng�y.
/// - Ghi ?i?m v�o DB b?c trong ExecuteTransactionAsync.
/// - Timezone: Asia/Ho_Chi_Minh (UTC+7).
/// </summary>
public interface IQuestService
{
    /// <summary>
    /// L?y danh s�ch quest ng�y h�m nay + tr?ng th�i Done.
    /// </summary>
    Task<Result<QuestTodayDto>> GetTodayAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Ho�n th�nh quest Check-in Daily (+5 points).
    /// Idempotent: ch? c?ng 1 l?n/ng�y.
    /// </summary>
    Task<Result> CompleteCheckInAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Ho�n th�nh quest Join Any Room (+5 points).
    /// G?i t? RoomService sau khi user join room th�nh c�ng.
    /// </summary>
    Task<Result> MarkJoinRoomAsync(Guid userId, Guid roomId, CancellationToken ct = default);

    /// <summary>
    /// Ho�n th�nh quest Invite Accepted (+10 points) cho ng??i g?i l?i m?i.
    /// G?i t? FriendService.Accept sau khi invite ???c ch?p nh?n.
    /// </summary>
    Task<Result> MarkInviteAcceptedAsync(Guid inviterId, Guid recipientId, CancellationToken ct = default);

    /// <summary>
    /// Ho�n th�nh quest Attend Event (+20 points).
    /// G?i t? EventService khi user check-in s? ki?n th�nh c�ng.
    /// </summary>
    Task<Result> MarkAttendEventAsync(Guid userId, Guid eventId, CancellationToken ct = default);
}
