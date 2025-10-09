namespace Services.Application.Quests;

/// <summary>
/// Daily Quests MVP: 4 quests (CheckInDaily, JoinAnyRoom, InviteAccepted, AttendEvent).
/// - Redis l?u flag hoàn thành per-user per-day (TTL ??n 00:00 VN hôm sau).
/// - Idempotent: 1 quest ch? complete 1 l?n/ngày.
/// - Ghi ?i?m vào DB b?c trong ExecuteTransactionAsync.
/// - Timezone: Asia/Ho_Chi_Minh (UTC+7).
/// </summary>
public interface IQuestService
{
    /// <summary>
    /// L?y danh sách quest ngày hôm nay + tr?ng thái Done.
    /// </summary>
    Task<Result<QuestTodayDto>> GetTodayAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Hoàn thành quest Check-in Daily (+5 points).
    /// Idempotent: ch? c?ng 1 l?n/ngày.
    /// </summary>
    Task<Result> CompleteCheckInAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Hoàn thành quest Join Any Room (+5 points).
    /// G?i t? RoomService sau khi user join room thành công.
    /// </summary>
    Task<Result> MarkJoinRoomAsync(Guid userId, Guid roomId, CancellationToken ct = default);

    /// <summary>
    /// Hoàn thành quest Invite Accepted (+10 points) cho ng??i g?i l?i m?i.
    /// G?i t? FriendService.Accept sau khi invite ???c ch?p nh?n.
    /// </summary>
    Task<Result> MarkInviteAcceptedAsync(Guid inviterId, Guid recipientId, CancellationToken ct = default);

    /// <summary>
    /// Hoàn thành quest Attend Event (+20 points).
    /// G?i t? EventService khi user check-in s? ki?n thành công.
    /// </summary>
    Task<Result> MarkAttendEventAsync(Guid userId, Guid eventId, CancellationToken ct = default);
}
