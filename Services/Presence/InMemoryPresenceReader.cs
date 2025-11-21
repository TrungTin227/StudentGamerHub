using BusinessObjects.Common.Results;
using DTOs.Presence;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Services.Presence;

/// <summary>
/// In-memory implementation of IPresenceReader for development or fallback scenarios.
/// </summary>
public sealed class InMemoryPresenceReader : IPresenceReader
{
    private readonly IMemoryCache _cache;
    private readonly PresenceOptions _options;
    private const string IndexKey = "presence:index";

    public InMemoryPresenceReader(IMemoryCache cache, IOptions<PresenceOptions> options)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public Task<Result<PresenceOnlineResponse>> GetOnlineAsync(PresenceOnlineQuery query, CancellationToken ct)
    {
        query ??= new PresenceOnlineQuery();
        var requestedSize = query.PageSize ?? _options.DefaultPageSize;
        
        if (requestedSize <= 0)
        {
            return Task.FromResult(Result<PresenceOnlineResponse>.Failure(
                new Error(Error.Codes.Validation, "pageSize must be positive")));
        }

        if (requestedSize > _options.MaxPageSize)
        {
            return Task.FromResult(Result<PresenceOnlineResponse>.Failure(
                new Error(Error.Codes.Validation, $"pageSize cannot exceed {_options.MaxPageSize}")));
        }

        var index = GetOrCreateIndex();
        var now = DateTimeOffset.UtcNow;
        var threshold = now.AddSeconds(-_options.GraceSeconds);

        // Clean up expired entries
        var expiredKeys = index.Where(kvp => kvp.Value < threshold).Select(kvp => kvp.Key).ToList();
        foreach (var key in expiredKeys)
        {
            index.TryRemove(key, out _);
        }

        // Get online users
        var onlineUsers = index
            .Where(kvp => kvp.Value >= threshold)
            .OrderBy(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key)
            .Select(kvp => (UserId: kvp.Key, Expiry: kvp.Value))
            .ToList();

        // Apply pagination
        var skip = 0;
        if (!string.IsNullOrWhiteSpace(query.Cursor))
        {
            // Simple cursor: just the userId
            if (Guid.TryParse(query.Cursor, out var cursorUserId))
            {
                var cursorIndex = onlineUsers.FindIndex(u => u.UserId == cursorUserId);
                if (cursorIndex >= 0)
                {
                    skip = cursorIndex + 1;
                }
            }
        }

        var page = onlineUsers.Skip(skip).Take(requestedSize + 1).ToList();
        var hasMore = page.Count > requestedSize;
        
        if (hasMore)
        {
            page = page.Take(requestedSize).ToList();
        }

        var items = page.Select(u =>
        {
            var ttl = (int)Math.Floor((u.Expiry - now).TotalSeconds);
            var lastSeen = _cache.Get<DateTimeOffset?>($"lastseen:{u.UserId}");
            return new PresenceSnapshotItem(u.UserId, true, lastSeen, ttl);
        }).ToList();

        var nextCursor = hasMore && items.Count > 0 ? items[^1].UserId.ToString() : null;

        return Task.FromResult(Result<PresenceOnlineResponse>.Success(
            new PresenceOnlineResponse(items, nextCursor)));
    }

    public Task<Result<IReadOnlyCollection<PresenceSnapshotItem>>> GetBatchAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken ct)
    {
        if (userIds is null)
        {
            return Task.FromResult(Result<IReadOnlyCollection<PresenceSnapshotItem>>.Failure(
                new Error(Error.Codes.Validation, "userIds are required")));
        }

        if (userIds.Count == 0)
        {
            return Task.FromResult(Result<IReadOnlyCollection<PresenceSnapshotItem>>.Failure(
                new Error(Error.Codes.Validation, "userIds cannot be empty")));
        }

        if (userIds.Count > _options.MaxBatchSize)
        {
            return Task.FromResult(Result<IReadOnlyCollection<PresenceSnapshotItem>>.Failure(
                new Error(Error.Codes.Validation, $"userIds cannot exceed {_options.MaxBatchSize}")));
        }

        var index = GetOrCreateIndex();
        var now = DateTimeOffset.UtcNow;
        var threshold = now.AddSeconds(-_options.GraceSeconds);

        var items = userIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Select(userId =>
            {
                var hasExpiry = index.TryGetValue(userId, out var expiry);
                var isOnline = hasExpiry && expiry >= threshold;
                var ttl = isOnline ? (int?)Math.Floor((expiry - now).TotalSeconds) : null;
                var lastSeen = _cache.Get<DateTimeOffset?>($"lastseen:{userId}");
                
                return new PresenceSnapshotItem(userId, isOnline, lastSeen, ttl);
            })
            .ToList();

        return Task.FromResult(Result<IReadOnlyCollection<PresenceSnapshotItem>>.Success(
            (IReadOnlyCollection<PresenceSnapshotItem>)items));
    }

    public Task<Result<PresenceSummaryResponse>> GetSummaryAsync(PresenceSummaryRequest request, CancellationToken ct)
    {
        request ??= new PresenceSummaryRequest(null);
        
        if (request.UserIds is { Count: > 0 })
        {
            var batchResult = GetBatchAsync(request.UserIds, ct).Result;
            if (!batchResult.IsSuccess)
            {
                return Task.FromResult(Result<PresenceSummaryResponse>.Failure(batchResult.Error));
            }

            var items = batchResult.Value;
            var online = items.Count(x => x.IsOnline);
            var offline = items.Count - online;
            
            return Task.FromResult(Result<PresenceSummaryResponse>.Success(
                new PresenceSummaryResponse(online, offline, items.Count, "batch")));
        }

        if (request.UserIds is { Count: 0 })
        {
            return Task.FromResult(Result<PresenceSummaryResponse>.Failure(
                new Error(Error.Codes.Validation, "userIds cannot be empty")));
        }

        var index = GetOrCreateIndex();
        var now = DateTimeOffset.UtcNow;
        var threshold = now.AddSeconds(-_options.GraceSeconds);

        var onlineCount = index.Count(kvp => kvp.Value >= threshold);

        return Task.FromResult(Result<PresenceSummaryResponse>.Success(
            new PresenceSummaryResponse(onlineCount, null, onlineCount, "global")));
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
