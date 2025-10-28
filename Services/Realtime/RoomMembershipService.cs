using BusinessObjects.Common;
using Microsoft.Extensions.Logging;
using Repositories.Interfaces;
using StackExchange.Redis;

namespace Services.Realtime;

/// <summary>
/// Provides Redis-backed membership checks for chat rooms.
/// </summary>
public sealed class RoomMembershipService : IRoomMembershipService
{
    private const string CacheKeyPrefix = "room:members";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);

    private readonly IConnectionMultiplexer _redis;
    private readonly IRoomQueryRepository _roomQuery;
    private readonly ILogger<RoomMembershipService> _logger;

    public RoomMembershipService(
        IConnectionMultiplexer redis,
        IRoomQueryRepository roomQuery,
        ILogger<RoomMembershipService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _roomQuery = roomQuery ?? throw new ArgumentNullException(nameof(roomQuery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> IsMemberAsync(
        Guid roomId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (roomId == Guid.Empty || userId == Guid.Empty)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var db = _redis.GetDatabase();
        var cacheKey = GetCacheKey(roomId);
        var memberValue = userId.ToString("D");

        try
        {
            if (await db.SetContainsAsync(cacheKey, memberValue).ConfigureAwait(false))
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to read membership cache for room {RoomId}. Falling back to database check.",
                roomId);
        }

        var member = await _roomQuery
            .GetMemberAsync(roomId, userId, cancellationToken)
            .ConfigureAwait(false);

        var isApproved = member is not null && member.Status == RoomMemberStatus.Approved;

        try
        {
            if (isApproved)
            {
                var batch = db.CreateBatch();
                var addTask = batch.SetAddAsync(cacheKey, memberValue);
                var expireTask = batch.KeyExpireAsync(cacheKey, CacheTtl);
                batch.Execute();
                await Task
                    .WhenAll(addTask, expireTask)
                    .ConfigureAwait(false);
            }
            else
            {
                await db.SetRemoveAsync(cacheKey, memberValue).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Failed to update membership cache for room {RoomId}.",
                roomId);
        }

        return isApproved;
    }

    private static string GetCacheKey(Guid roomId) =>
        FormattableString.Invariant($"{CacheKeyPrefix}:{roomId:D}");
}
