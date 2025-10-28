using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Services.Realtime;

/// <summary>
/// Implements a distributed sliding window rate limiter using Redis sorted sets.
/// </summary>
public sealed class RedisSlidingWindowRateLimiter : IRateLimiter
{
    private const string RateLimitKeyPrefix = "chat:rl";

    private static readonly LuaScript SlidingWindowScript = LuaScript.Prepare(@"
redis.call('ZREMRANGEBYSCORE', @key, '-inf', @threshold)
redis.call('ZADD', @key, @now, @member)
redis.call('PEXPIRE', @key, @ttl)
return redis.call('ZCARD', @key)");

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisSlidingWindowRateLimiter> _logger;

    public RedisSlidingWindowRateLimiter(
        IConnectionMultiplexer redis,
        ILogger<RedisSlidingWindowRateLimiter> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> IsAllowedAsync(
        Guid userId,
        int maxEvents,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId is required for rate limiting.", nameof(userId));
        }

        if (maxEvents <= 0 || window <= TimeSpan.Zero)
        {
            return true;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var db = _redis.GetDatabase();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var member = $"{now}:{Guid.NewGuid():N}";
        var key = FormattableString.Invariant($"{RateLimitKeyPrefix}:{userId:D}");
        var ttlMilliseconds = Math.Max((long)window.TotalMilliseconds, 1);

        var threshold = now - ttlMilliseconds;

        try
        {
            var result = (long)await db
                .ScriptEvaluateAsync(
                    SlidingWindowScript,
                    new
                    {
                        key = (RedisKey)key,
                        now,
                        member,
                        ttl = ttlMilliseconds,
                        threshold
                    })
                .ConfigureAwait(false);

            return result <= maxEvents;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to enforce rate limit for user {UserId}.",
                userId);
            // Fail open to avoid blocking chat on transient Redis issues.
            return true;
        }
    }
}
