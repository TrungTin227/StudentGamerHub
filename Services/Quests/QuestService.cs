using Services.Application.Quests;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Services.Quests;

public sealed class QuestService : IQuestService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IUnitOfWork _uow;
    private readonly AppDbContext _db;

    // Quest definitions (MVP hardcode; config hoá sau)
    private static readonly QuestDefinition[] _allQuests = new[]
    {
        new QuestDefinition("CHECK_IN_DAILY", "Check-in hôm nay", 5),
        new QuestDefinition("JOIN_ANY_ROOM", "Tham gia bất kì phòng", 5),
        new QuestDefinition("INVITE_ACCEPTED", "Lời mời bạn chấp nhận", 10),
        new QuestDefinition("ATTEND_EVENT", "Điểm danh sự kiện", 20)
    };

    // Timezone VN (Asia/Ho_Chi_Minh) = UTC+7
    private static readonly TimeSpan _vnOffset = TimeSpan.FromHours(7);

    public QuestService(
        IConnectionMultiplexer redis,
        IUnitOfWork uow,
        AppDbContext db)
    {
        _redis = redis;
        _uow = uow;
        _db = db;
    }

    // ========================== PUBLIC API ==========================

    public async Task<Result<QuestTodayDto>> GetTodayAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return Result<QuestTodayDto>.Failure(new Error(Error.Codes.Validation, "User ID is required"));

        return await ResultExtensions.TryAsync(async () =>
        {
            var (dateStr, _, _) = GetVnDayInfo();
            var db = _redis.GetDatabase();

            // 1) Ki?m tra flag Done cho t?t c? quests
            var tasks = _allQuests.Select(q => db.KeyExistsAsync(BuildQuestKey(userId, dateStr, q.Code)));
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            var items = _allQuests
                .Zip(results, (q, done) => new QuestItemDto(q.Code, q.Title, q.Reward, done))
                .ToArray();

            // 2) L?y Points hi?n t?i c?a user t? DB
            var user = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Points })
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            var currentPoints = user?.Points ?? 0;

            return new QuestTodayDto(currentPoints, items);
        });
    }

    public Task<Result> CompleteCheckInAsync(Guid userId, CancellationToken ct = default)
        => CompleteQuestAsync(userId, "CHECK_IN_DAILY", null, ct);

    public Task<Result> MarkJoinRoomAsync(Guid userId, Guid roomId, CancellationToken ct = default)
        => CompleteQuestAsync(userId, "JOIN_ANY_ROOM", $"Room:{roomId}", ct);

    public Task<Result> MarkInviteAcceptedAsync(Guid inviterId, Guid recipientId, CancellationToken ct = default)
        => CompleteQuestAsync(inviterId, "INVITE_ACCEPTED", $"Recipient:{recipientId}", ct);

    public Task<Result> MarkAttendEventAsync(Guid userId, Guid eventId, CancellationToken ct = default)
        => CompleteQuestAsync(userId, "ATTEND_EVENT", $"Event:{eventId}", ct);

    // ========================== CORE LOGIC ==========================

    /// <summary>
    /// Core idempotent quest completion v?i SET NX + rollback flag n?u DB fail:
    /// 1. SET NX flag Redis (TTL ??n 00:00 VN hôm sau)
    /// 2. N?u FALSE ? ?ã complete hôm nay ? return Validation error
    /// 3. N?u TRUE ? c?ng ?i?m vào DB (transaction)
    /// 4. N?u DB commit OK ? INCR counter (dashboard analytics)
    /// 5. N?u DB commit FAIL ? best-effort DEL flag + return Unexpected
    /// </summary>
    private async Task<Result> CompleteQuestAsync(
        Guid userId,
        string questCode,
        string? metadata,
        CancellationToken ct)
    {
        if (userId == Guid.Empty)
            return Result.Failure(new Error(Error.Codes.Validation, "User ID is required"));

        var quest = _allQuests.FirstOrDefault(q => q.Code == questCode);
        if (quest is null)
            return Result.Failure(new Error(Error.Codes.NotFound, $"Quest '{questCode}' not found"));

        try
        {
            var (dateStr, nextMidnightVN, minuteStr) = GetVnDayInfo();
            var db = _redis.GetDatabase();
            var key = BuildQuestKey(userId, dateStr, questCode);
            var ttl = nextMidnightVN - DateTimeOffset.UtcNow;

            // ? STEP 1: Idempotent check v?i SET NX
            var flagSet = await db.StringSetAsync(key, "1", ttl, When.NotExists).ConfigureAwait(false);
            if (!flagSet)
            {
                // ?ã complete hôm nay
                return Result.Failure(new Error(Error.Codes.Validation, "Quest already completed today"));
            }

            // ? STEP 2: C?ng ?i?m vào DB (transaction)
            var pointsResult = await _uow.ExecuteTransactionAsync(async ctk =>
            {
                // Ki?m tra user t?n t?i + c?ng ?i?m
                var user = await _db.Users
                    .Where(u => u.Id == userId)
                    .FirstOrDefaultAsync(ctk)
                    .ConfigureAwait(false);

                if (user is null)
                {
                    return Result.Failure(new Error(Error.Codes.NotFound, "User not found"));
                }

                user.Points += quest.Reward;
                user.UpdatedAtUtc = DateTime.UtcNow;

                await _db.SaveChangesAsync(ctk).ConfigureAwait(false);
                return Result.Success();
            }, ct: ct).ConfigureAwait(false);

            // ? STEP 3: X? lý k?t qu? transaction
            if (pointsResult.IsSuccess)
            {
                // DB commit OK ? INCR counter (best-effort)
                try
                {
                    var counterKey = BuildCounterKey(minuteStr);
                    await db.StringIncrementAsync(counterKey).ConfigureAwait(false);
                    await db.KeyExpireAsync(counterKey, TimeSpan.FromHours(2)).ConfigureAwait(false);
                }
                catch
                {
                    // Best-effort: không fail n?u counter l?i
                }

                return Result.Success();
            }
            else
            {
                // ? DB commit FAIL ? rollback flag (best-effort)
                try
                {
                    await db.KeyDeleteAsync(key).ConfigureAwait(false);
                }
                catch
                {
                    // Best-effort: log n?u c?n
                }

                return pointsResult;
            }
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error(Error.Codes.Unexpected, $"Quest completion failed: {ex.Message}"));
        }
    }

    // ========================== HELPERS ==========================

    /// <summary>
    /// Tính ngày VN hi?n t?i + midnight ti?p theo + minute string (cho counter).
    /// Tr? v?: (dateStr: "yyyyMMdd", nextMidnightVN: DateTimeOffset, minuteStr: "yyyyMMddHHmm")
    /// </summary>
    private static (string dateStr, DateTimeOffset nextMidnightVN, string minuteStr) GetVnDayInfo()
    {
        var nowVN = DateTimeOffset.UtcNow.ToOffset(_vnOffset);
        var todayVN = nowVN.Date;
        var nextMidnightVN = new DateTimeOffset(todayVN.AddDays(1), _vnOffset);

        var dateStr = todayVN.ToString("yyyyMMdd");
        var minuteStr = nowVN.ToString("yyyyMMddHHmm");

        return (dateStr, nextMidnightVN, minuteStr);
    }

    /// <summary>
    /// Redis key cho quest flag: q:{yyyyMMdd}:{userId}:{questCode} = "1" (TTL ??n 00:00 VN hôm sau)
    /// </summary>
    private static RedisKey BuildQuestKey(Guid userId, string dateStr, string questCode)
        => $"q:{dateStr}:{userId}:{questCode}";

    /// <summary>
    /// Redis key cho counter analytics (dashboard): qc:done:{yyyyMMddHHmm} = incr (TTL 2h)
    /// </summary>
    private static RedisKey BuildCounterKey(string minuteStr)
        => $"qc:done:{minuteStr}";

    // ========================== MODELS ==========================

    private sealed record QuestDefinition(string Code, string Title, int Reward);
}
