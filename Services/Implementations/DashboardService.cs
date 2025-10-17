using Application.Friends;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.Application.Quests;
using StackExchange.Redis;

namespace Services.Implementations;

/// <summary>
/// Dashboard service implementation
/// Aggregates Points, Quests, Events, and Activity for today (VN timezone)
/// NOTE:
/// - All EF Core queries run sequentially to avoid concurrent-DbContext issues.
/// - Redis operations are independent and can be run after EF parts.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;
    private readonly IEventQueryRepository _eventQueries;
    private readonly IFriendLinkQuerRepository _friendQueries;
    private readonly IPresenceService _presence;
    private readonly IQuestService _quests;
    private readonly IConnectionMultiplexer _redis;
    private readonly TimeProvider _time;
    private readonly ILogger<DashboardService> _logger;

    // VN timezone offset (UTC+7)
    private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    public DashboardService(
        AppDbContext db,
        IEventQueryRepository eventQueries,
        IFriendLinkQuerRepository friendQueries,
        IPresenceService presence,
        IQuestService quests,
        IConnectionMultiplexer redis,
        TimeProvider? time = null,
        ILogger<DashboardService>? logger = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _eventQueries = eventQueries ?? throw new ArgumentNullException(nameof(eventQueries));
        _friendQueries = friendQueries ?? throw new ArgumentNullException(nameof(friendQueries));
        _presence = presence ?? throw new ArgumentNullException(nameof(presence));
        _quests = quests ?? throw new ArgumentNullException(nameof(quests));
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _time = time ?? TimeProvider.System;
        _logger = logger ?? NullLogger<DashboardService>.Instance;
    }

    public async Task<Result<DashboardTodayDto>> GetTodayAsync(Guid userId, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return Result<DashboardTodayDto>.Failure(
            new Error(Error.Codes.Cancelled, "Request was cancelled."));

        if (userId == Guid.Empty)
            return Result<DashboardTodayDto>.Failure(
                new Error(Error.Codes.Validation, "User ID is required"));

        try
        {
            // ===== Calculate VN day range =====
            var nowUtc = _time.GetUtcNow().UtcDateTime;   // DateTime (UTC)
            var nowVn = nowUtc + VnOffset;                // local VN time (no tz info)
            var startVn = nowVn.Date;                     // 00:00 VN
            var endVn = startVn.AddDays(1);               // 00:00 next VN day

            var startUtc = DateTime.SpecifyKind(startVn - VnOffset, DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(endVn - VnOffset, DateTimeKind.Utc);

            // ===== EF queries (SEQUENTIAL) =====
            // 1) Points
            var points = await GetUserPointsAsync(userId, ct).ConfigureAwait(false);

            // 2) Quests today
            var questsResult = await _quests.GetTodayAsync(userId, ct).ConfigureAwait(false);
            if (questsResult.IsFailure)
                return Result<DashboardTodayDto>.Failure(questsResult.Error);

            // 3) Events in VN "today"
            var events = await GetEventsTodayAsync(startUtc, endUtc, ct).ConfigureAwait(false);

            // 4) Activity (split: friends = DB, questsDone = Redis)
            var onlineFriends = await GetOnlineFriendsCountAsync(userId, ct).ConfigureAwait(false);

            // ===== Redis (independent) =====
            var questsDone = await GetQuestsDoneLast60MinutesAsync(nowVn, ct).ConfigureAwait(false);

            var activity = new ActivityDto(onlineFriends, questsDone);

            var dto = new DashboardTodayDto(
                Points: points,
                Quests: questsResult.Value!,
                EventsToday: events,
                Activity: activity
            );

            return Result<DashboardTodayDto>.Success(dto);
        }
        catch (OperationCanceledException)
        {
            return Result<DashboardTodayDto>.Failure(
                new Error(Error.Codes.Cancelled, "Request was cancelled."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get dashboard data for user {UserId}", userId);
            return Result<DashboardTodayDto>.Failure(
                new Error(Error.Codes.Unexpected, $"Failed to get dashboard data: {ex.Message}"));
        }
    }

    // ====================== Private Helpers ======================

    /// <summary>
    /// Get user points from database (read-only query)
    /// </summary>
    private async Task<int> GetUserPointsAsync(Guid userId, CancellationToken ct)
    {
        var points = await _db.Users
            .AsNoTracking()
            .Where(u => !u.IsDeleted && u.Id == userId)
            .Select(u => (int?)u.Points)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return points ?? 0;
    }

    /// <summary>
    /// Get events starting today in VN timezone
    /// Maps Event entities to EventBriefDto
    /// </summary>
    private async Task<EventBriefDto[]> GetEventsTodayAsync(
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct)
    {
        var events = await _eventQueries
            .GetEventsStartingInRangeUtcAsync(startUtc, endUtc, ct)
            .ConfigureAwait(false);

        // Map Event -> EventBriefDto
        return events.Select(e => new EventBriefDto(
                Id: e.Id,
                Title: e.Title,
                StartsAt: e.StartsAt,
                EndsAt: e.EndsAt,
                Location: e.Location,
                Mode: e.Mode.ToString()))
            .ToArray();
    }

    /// <summary>
    /// Get count of online friends using batch presence check
    /// </summary>
    private async Task<int> GetOnlineFriendsCountAsync(Guid userId, CancellationToken ct)
    {
        // Using repository (likely EF). Run sequentially relative to other EF calls.
        var friendIds = await _friendQueries
            .GetAcceptedFriendIdsAsync(userId, ct)
            .ConfigureAwait(false);

        if (friendIds.Count == 0) return 0;

        var presenceResult = await _presence
            .BatchIsOnlineAsync(friendIds, ct)
            .ConfigureAwait(false);

        if (presenceResult.IsFailure || presenceResult.Value is null) return 0;

        return presenceResult.Value.Values.Count(isOnline => isOnline);
    }

    /// <summary>
    /// Get total quests completed in last 60 minutes (Redis MGET)
    /// Key format: "qc:done:{yyyyMMddHHmm}" in VN time.
    /// </summary>
    private async Task<int> GetQuestsDoneLast60MinutesAsync(DateTime nowVn, CancellationToken ct)
    {
        var db = _redis.GetDatabase();

        // Build minute keys for [now .. now-59m] in VN time
        var keys = new RedisKey[60];
        for (int i = 0; i < 60; i++)
        {
            var minuteVn = nowVn.AddMinutes(-i);
            keys[i] = $"qc:done:{minuteVn:yyyyMMddHHmm}";
        }

        // Single MGET
        var values = await db.StringGetAsync(keys).ConfigureAwait(false);

        var total = 0;
        foreach (var v in values)
        {
            if (v.HasValue && v.TryParse(out int count))
                total += count;
        }

        return total;
    }
}

/// <summary>
/// Fallback logger when ILogger isn't provided.
/// </summary>
file sealed class NullLogger<T> : ILogger<T>, IDisposable
{
    public static readonly NullLogger<T> Instance = new();
    private NullLogger() { }
    public IDisposable BeginScope<TState>(TState state) => this;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    { }
    public void Dispose() { }
}
