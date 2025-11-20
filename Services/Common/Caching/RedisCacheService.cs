using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace Services.Common.Caching;

/// <summary>
/// Redis-based implementation of ICacheService.
/// </summary>
public sealed class RedisCacheService : ICacheService
{
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(10);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(
        IConnectionMultiplexer redis,
        ILogger<RedisCacheService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key).ConfigureAwait(false);

            if (!value.HasValue)
            {
                _logger.LogDebug("Cache MISS for key: {CacheKey}", key);
                return null;
            }

            _logger.LogDebug("Cache HIT for key: {CacheKey}", key);
            return JsonSerializer.Deserialize<T>(value.ToString(), JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get cache for key: {CacheKey}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        try
        {
            var db = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(value, JsonOptions);
            var ttl = expiration ?? DefaultExpiration;

            await db.StringSetAsync(key, json, ttl).ConfigureAwait(false);
            _logger.LogDebug("Cache SET for key: {CacheKey}, TTL: {TTL}", key, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set cache for key: {CacheKey}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key).ConfigureAwait(false);
            _logger.LogDebug("Cache DELETE for key: {CacheKey}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove cache for key: {CacheKey}", key);
        }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var db = _redis.GetDatabase();

            await foreach (var key in server.KeysAsync(pattern: pattern))
            {
                await db.KeyDeleteAsync(key).ConfigureAwait(false);
            }

            _logger.LogDebug("Cache DELETE by pattern: {Pattern}", pattern);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove cache by pattern: {Pattern}", pattern);
        }
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken ct = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        var cached = await GetAsync<T>(key, ct).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        var value = await factory(ct).ConfigureAwait(false);
        await SetAsync(key, value, expiration, ct).ConfigureAwait(false);
        return value;
    }
}
