using Application.Friends;
using BusinessObjects.Common.Results;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Services.Presence;

/// <summary>
/// In-memory implementation of IPresenceService for development or fallback scenarios.
/// </summary>
public sealed class InMemoryPresenceService : IPresenceService
{
    private readonly IMemoryCache _cache;
    private readonly PresenceOptions _options;
    private const string IndexKey = "presence:index";

    public InMemoryPresenceService(IMemoryCache cache, IOptions<PresenceOptions> options)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<Result> HeartbeatAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            return Task.FromResult(Result.Failure(new Error(Error.Codes.Validation, "User id is required")));
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            var ttl = TimeSpan.FromSeconds(_options.TtlSeconds);
            var expiry = now.Add(ttl);

            // Update presence index
            var index = GetOrCreateIndex();
            index[userId] = expiry;

            // Update last seen
            _cache.Set($"lastseen:{userId}", now, TimeSpan.FromDays(30));

            // Update presence key with TTL
            _cache.Set($"presence:{userId}", "1", ttl);

            // Clean up expired entries
            var threshold = now.AddSeconds(-_options.GraceSeconds);
            var expiredKeys = index.Where(kvp => kvp.Value < threshold).Select(kvp => kvp.Key).ToList();
            foreach (var key in expiredKeys)
            {
                index.TryRemove(key, out _);
            }

            return Task.FromResult(Result.Success());
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure(new Error(Error.Codes.Unexpected, ex.Message)));
        }
    }

    public Task<Result<bool>> IsOnlineAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            return Task.FromResult(Result<bool>.Failure(new Error(Error.Codes.Validation, "User id is required")));
        }

        try
        {
            var index = GetOrCreateIndex();
            var now = DateTimeOffset.UtcNow;
            var threshold = now.AddSeconds(-_options.GraceSeconds);

            if (index.TryGetValue(userId, out var expiry))
            {
                if (expiry >= threshold)
                {
                    return Task.FromResult(Result<bool>.Success(true));
                }
                
                // Remove expired entry
                index.TryRemove(userId, out _);
            }

            return Task.FromResult(Result<bool>.Success(false));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<bool>.Failure(new Error(Error.Codes.Unexpected, ex.Message)));
        }
    }

    public Task<Result<IReadOnlyDictionary<Guid, bool>>> BatchIsOnlineAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken ct = default)
    {
        if (userIds is null)
        {
            return Task.FromResult(Result<IReadOnlyDictionary<Guid, bool>>.Failure(
                new Error(Error.Codes.Validation, "User ids are required")));
        }

        try
        {
            var distinctIds = userIds.Where(id => id != Guid.Empty).Distinct().ToArray();
            if (distinctIds.Length == 0)
            {
                return Task.FromResult(Result<IReadOnlyDictionary<Guid, bool>>.Success(
                    (IReadOnlyDictionary<Guid, bool>)new Dictionary<Guid, bool>()));
            }

            var index = GetOrCreateIndex();
            var now = DateTimeOffset.UtcNow;
            var threshold = now.AddSeconds(-_options.GraceSeconds);

            var result = distinctIds.ToDictionary(
                userId => userId,
                userId =>
                {
                    if (index.TryGetValue(userId, out var expiry))
                    {
                        return expiry >= threshold;
                    }
                    return false;
                });

            return Task.FromResult(Result<IReadOnlyDictionary<Guid, bool>>.Success(
                (IReadOnlyDictionary<Guid, bool>)result));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<IReadOnlyDictionary<Guid, bool>>.Failure(
                new Error(Error.Codes.Unexpected, ex.Message)));
        }
    }

    private ConcurrentDictionary<Guid, DateTimeOffset> GetOrCreateIndex()
    {
        return _cache.GetOrCreate(IndexKey, entry =>
        {
            entry.Priority = CacheItemPriority.NeverRemove;
            return new ConcurrentDictionary<Guid, DateTimeOffset>();
        })!;
    }
}
