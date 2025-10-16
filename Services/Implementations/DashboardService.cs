using Application.Friends;
using Microsoft.EntityFrameworkCore;
using Services.Application.Quests;
using Services.Common.Extensions;
using StackExchange.Redis;

namespace Services.Implementations;

/// <summary>
/// Dashboard service implementation
/// Aggregates Points, Quests, Events, and Activity for today (VN timezone)
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;
    private readonly IEventQueryRepository _eventQueries;
    private readonly IFriendLinkQuerRepository _friendQueries;
    private readonly IPresenceService _presence;
    private readonly IQuestService _quests;
    private readonly IConnectionMultiplexer _redis;

    // VN timezone offset
    private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    public DashboardService(
        AppDbContext db,
        IEventQueryRepository eventQueries,
        IFriendLinkQuerRepository friendQueries,
        IPresenceService presence,
        IQuestService quests,
        IConnectionMultiplexer redis)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _eventQueries = eventQueries ?? throw new ArgumentNullException(nameof(eventQueries));
        _friendQueries = friendQueries ?? throw new ArgumentNullException(nameof(friendQueries));
        _presence = presence ?? throw new ArgumentNullException(nameof(presence));
        _quests = quests ?? throw new ArgumentNullException(nameof(quests));
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
    }

    public async Task<Result<DashboardTodayDto>> GetTodayAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            return Result<DashboardTodayDto>.Failure(
                new Error(Error.Codes.Validation, "User ID is required"));
        }

        try
        {
            // Calculate VN date range for today
            var nowUtc = DateTime.UtcNow;
            var nowVn = nowUtc + VnOffset;
            var startVn = nowVn.Date; // 00:00 VN time
            var endVn = startVn.AddDays(1); // 00:00 VN time next day

            var startUtc = DateTime.SpecifyKind(startVn - VnOffset, DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(endVn - VnOffset, DateTimeKind.Utc);

            // Parallel data fetching
            var pointsTask = GetUserPointsAsync(userId, ct);
            var questsTask = _quests.GetTodayAsync(userId, ct);
            var eventsTask = GetEventsTodayAsync(startUtc, endUtc, ct);
            var activityTask = GetActivityAsync(userId, nowVn, ct);

            await Task.WhenAll(pointsTask, questsTask, eventsTask, activityTask).ConfigureAwait(false);

            var points = await pointsTask;
            var questsResult = await questsTask;
            var events = await eventsTask;
            var activity = await activityTask;

            // Handle quest service result
            if (questsResult.IsFailure)
            {
                return Result<DashboardTodayDto>.Failure(questsResult.Error);
            }

            var dashboard = new DashboardTodayDto(
                Points: points,
                Quests: questsResult.Value!,
                EventsToday: events,
                Activity: activity
            );

            return Result<DashboardTodayDto>.Success(dashboard);
        }
        catch (Exception ex)
        {
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
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Points })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return user?.Points ?? 0;
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
        return events
            .Select(e => new EventBriefDto(
                Id: e.Id,
                Title: e.Title,
                StartsAt: e.StartsAt,
                EndsAt: e.EndsAt,
                Location: e.Location,
                Mode: e.Mode.ToString() // EventMode enum -> string
            ))
            .ToArray();
    }

    /// <summary>
    /// Get activity metrics: online friends count and quests completed in last 60 minutes
    /// </summary>
    private async Task<ActivityDto> GetActivityAsync(
        Guid userId,
        DateTime nowVn,
        CancellationToken ct)
    {
        // Get online friends count
        var onlineFriendsTask = GetOnlineFriendsCountAsync(userId, ct);
        
        // Get quests done in last 60 minutes
        var questsDone60mTask = GetQuestsDoneLast60MinutesAsync(nowVn, ct);

        await Task.WhenAll(onlineFriendsTask, questsDone60mTask).ConfigureAwait(false);

        var onlineFriends = await onlineFriendsTask;
        var questsDone = await questsDone60mTask;

        return new ActivityDto(
            OnlineFriends: onlineFriends,
            QuestsDoneLast60m: questsDone
        );
    }

    /// <summary>
    /// Get count of online friends using batch presence check
    /// Uses single Redis pipeline for efficiency
    /// </summary>
    private async Task<int> GetOnlineFriendsCountAsync(Guid userId, CancellationToken ct)
    {
        // Get friend IDs
        var friendIds = await _friendQueries
            .GetAcceptedFriendIdsAsync(userId, ct)
            .ConfigureAwait(false);

        if (friendIds.Count == 0)
        {
            return 0;
        }

        // Batch presence check - single pipeline call
        var presenceResult = await _presence
            .BatchIsOnlineAsync(friendIds, ct)
            .ConfigureAwait(false);

        if (presenceResult.IsFailure)
        {
            return 0; // Return 0 on error, don't fail the entire dashboard
        }

        // Count online friends
        return presenceResult.Value!.Values.Count(isOnline => isOnline);
    }

    /// <summary>
    /// Get total quests completed in last 60 minutes
    /// Uses Redis MGET batch operation for efficiency
    /// Key format: "qc:done:{yyyyMMddHHmm}"
    /// </summary>
    private async Task<int> GetQuestsDoneLast60MinutesAsync(DateTime nowVn, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        
        // Generate 60 minute keys (current minute and 59 minutes back)
        var keys = new List<RedisKey>(60);
        for (int i = 0; i < 60; i++)
        {
            var minuteVn = nowVn.AddMinutes(-i);
            var minuteKey = $"qc:done:{minuteVn:yyyyMMddHHmm}";
            keys.Add(minuteKey);
        }

        // MGET batch operation - single Redis call
        var values = await db.StringGetAsync(keys.ToArray()).ConfigureAwait(false);

        // Sum all counts (null values are treated as 0)
        int total = 0;
        foreach (var value in values)
        {
            if (value.HasValue && value.TryParse(out int count))
            {
                total += count;
            }
        }

        return total;
    }
}
