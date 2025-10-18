using System.Globalization;
using System.Linq;
using System.Text;
using BusinessObjects.Common.Results;
using DTOs.Presence;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Services.Presence;

public sealed class RedisPresenceReader : IPresenceReader
{
    private readonly IConnectionMultiplexer _redis;
    private readonly PresenceOptions _options;
    private readonly string _prefix;

    public RedisPresenceReader(IConnectionMultiplexer redis, IOptions<PresenceOptions> options)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _prefix = PresenceKeyHelper.NormalizePrefix(_options.KeyPrefix);
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

        if (!TryDecodeCursor(query.Cursor, out var cursorScore, out var cursorMember))
        {
            return Task.FromResult(Result<PresenceOnlineResponse>.Failure(
                new Error(Error.Codes.Validation, "cursor is invalid")));
        }

        return ResultExtensions.TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();

            var db = _redis.GetDatabase();
            var now = DateTimeOffset.UtcNow;
            var threshold = now.AddSeconds(-_options.GraceSeconds).ToUnixTimeMilliseconds();
            var indexKey = PresenceKeyHelper.GetIndexKey(_prefix);

            await db.SortedSetRemoveRangeByScoreAsync(indexKey, double.NegativeInfinity, threshold - 1)
                .ConfigureAwait(false);

            var startScore = cursorScore.HasValue ? Math.Max(cursorScore.Value, threshold) : threshold;
            var fetchSize = requestedSize + 1;
            var rawEntries = await db.SortedSetRangeByScoreWithScoresAsync(
                    indexKey,
                    startScore,
                    double.PositiveInfinity,
                    Exclude.None,
                    Order.Ascending,
                    0,
                    fetchSize + (cursorMember is null ? 0 : 1))
                .ConfigureAwait(false);

            var filteredEntries = new List<SortedSetEntry>(rawEntries.Length);
            foreach (var entry in rawEntries)
            {
                if (entry.Element.IsNullOrEmpty)
                {
                    continue;
                }

                var element = entry.Element.ToString();
                if (!Guid.TryParse(element, out _))
                {
                    continue;
                }

                var expiryMs = ToMilliseconds(entry.Score);
                if (expiryMs < threshold)
                {
                    continue;
                }

                if (cursorScore.HasValue)
                {
                    if (expiryMs < cursorScore.Value)
                    {
                        continue;
                    }

                    if (expiryMs == cursorScore.Value && cursorMember is not null)
                    {
                        var comparison = string.CompareOrdinal(element, cursorMember);
                        if (comparison <= 0)
                        {
                            continue;
                        }
                    }
                }

                filteredEntries.Add(entry);
                if (filteredEntries.Count >= requestedSize + 1)
                {
                    break;
                }
            }

            var hasMore = filteredEntries.Count > requestedSize;
            if (hasMore)
            {
                filteredEntries = filteredEntries.Take(requestedSize).ToList();
            }

            var userEntries = filteredEntries.Take(requestedSize).ToArray();
            if (userEntries.Length == 0)
            {
                return new PresenceOnlineResponse(Array.Empty<PresenceSnapshotItem>(), null);
            }

            var batch = db.CreateBatch();
            var lastSeenTasks = new Dictionary<Guid, Task<RedisValue>>(userEntries.Length);
            foreach (var entry in userEntries)
            {
                var userId = Guid.Parse(entry.Element.ToString());
                lastSeenTasks[userId] = batch.StringGetAsync(PresenceKeyHelper.GetLastSeenKey(_prefix, userId));
            }

            batch.Execute();
            await Task.WhenAll(lastSeenTasks.Values).ConfigureAwait(false);

            var items = new List<PresenceSnapshotItem>(userEntries.Length);
            foreach (var entry in userEntries)
            {
                var userId = Guid.Parse(entry.Element.ToString());
                var snapshot = CreateSnapshot(userId, entry.Score, now, lastSeenTasks[userId].Result);
                items.Add(snapshot);
            }

            var nextCursor = hasMore
                ? EncodeCursor(ToMilliseconds(userEntries[^1].Score), userEntries[^1].Element.ToString())
                : null;

            return new PresenceOnlineResponse(items, nextCursor);
        });
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

        var orderedUniqueIds = new List<Guid>(userIds.Count);
        var seen = new HashSet<Guid>();
        foreach (var id in userIds)
        {
            if (id == Guid.Empty || !seen.Add(id))
            {
                continue;
            }

            orderedUniqueIds.Add(id);
        }

        if (orderedUniqueIds.Count == 0)
        {
            return Task.FromResult(Result<IReadOnlyCollection<PresenceSnapshotItem>>.Failure(
                new Error(Error.Codes.Validation, "userIds are invalid")));
        }

        return ResultExtensions.TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();

            var db = _redis.GetDatabase();
            var batch = db.CreateBatch();
            var scoreTasks = new Dictionary<Guid, Task<double?>>(orderedUniqueIds.Count);
            var lastSeenTasks = new Dictionary<Guid, Task<RedisValue>>(orderedUniqueIds.Count);
            var indexKey = PresenceKeyHelper.GetIndexKey(_prefix);
            foreach (var id in orderedUniqueIds)
            {
                scoreTasks[id] = batch.SortedSetScoreAsync(indexKey, PresenceKeyHelper.GetMember(id));
                lastSeenTasks[id] = batch.StringGetAsync(PresenceKeyHelper.GetLastSeenKey(_prefix, id));
            }

            batch.Execute();
            await Task.WhenAll(scoreTasks.Values).ConfigureAwait(false);
            await Task.WhenAll(lastSeenTasks.Values).ConfigureAwait(false);

            var now = DateTimeOffset.UtcNow;
            var items = new List<PresenceSnapshotItem>(orderedUniqueIds.Count);
            foreach (var id in orderedUniqueIds)
            {
                var score = scoreTasks[id].Result;
                var lastSeen = lastSeenTasks[id].Result;
                var snapshot = CreateSnapshot(id, score, now, lastSeen);
                items.Add(snapshot);
            }

            return (IReadOnlyCollection<PresenceSnapshotItem>)items;
        });
    }

    public async Task<Result<PresenceSummaryResponse>> GetSummaryAsync(PresenceSummaryRequest request, CancellationToken ct)
    {
        request ??= new PresenceSummaryRequest(null);
        if (request.UserIds is { Count: > 0 })
        {
            var batchResult = await GetBatchAsync(request.UserIds, ct).ConfigureAwait(false);
            if (!batchResult.IsSuccess)
            {
                return Result<PresenceSummaryResponse>.Failure(batchResult.Error);
            }

            var items = batchResult.Value;
            var online = items.Count(x => x.IsOnline);
            var offline = items.Count - online;
            return Result<PresenceSummaryResponse>.Success(
                new PresenceSummaryResponse(online, offline, items.Count, "batch"));
        }

        if (request.UserIds is { Count: 0 })
        {
            return Result<PresenceSummaryResponse>.Failure(
                new Error(Error.Codes.Validation, "userIds cannot be empty"));
        }

        return await ResultExtensions.TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();

            var db = _redis.GetDatabase();
            var now = DateTimeOffset.UtcNow;
            var threshold = now.AddSeconds(-_options.GraceSeconds).ToUnixTimeMilliseconds();
            var indexKey = PresenceKeyHelper.GetIndexKey(_prefix);

            await db.SortedSetRemoveRangeByScoreAsync(indexKey, double.NegativeInfinity, threshold - 1)
                .ConfigureAwait(false);

            var count = await db.SortedSetLengthAsync(indexKey, threshold, double.PositiveInfinity)
                .ConfigureAwait(false);
            var onlineCount = count > int.MaxValue ? int.MaxValue : (int)count;

            return new PresenceSummaryResponse(onlineCount, null, onlineCount, "global");
        }).ConfigureAwait(false);
    }

    private PresenceSnapshotItem CreateSnapshot(Guid userId, double? score, DateTimeOffset now, RedisValue lastSeenValue)
    {
        if (!score.HasValue)
        {
            return new PresenceSnapshotItem(userId, false, ParseLastSeen(lastSeenValue), null);
        }

        var expiry = DateTimeOffset.FromUnixTimeMilliseconds(ToMilliseconds(score.Value));
        var secondsRemaining = (int)Math.Floor((expiry - now).TotalSeconds);
        var isOnline = secondsRemaining >= -_options.GraceSeconds;
        var ttl = isOnline ? secondsRemaining : (int?)null;
        var lastSeen = ParseLastSeen(lastSeenValue);

        return new PresenceSnapshotItem(userId, isOnline, lastSeen, ttl);
    }

    private static DateTimeOffset? ParseLastSeen(RedisValue value)
    {
        if (value.IsNullOrEmpty)
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        return null;
    }

    private static bool TryDecodeCursor(string? cursor, out long? score, out string? member)
    {
        score = null;
        member = null;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return true;
        }

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                return false;
            }

            if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedScore))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(parts[1]))
            {
                return false;
            }

            score = parsedScore;
            member = parts[1];
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? EncodeCursor(long score, string? member)
    {
        if (string.IsNullOrWhiteSpace(member))
        {
            return null;
        }

        var payload = FormattableString.Invariant($"{score}|{member}");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    private static long ToMilliseconds(double score) => (long)Math.Round(score, MidpointRounding.AwayFromZero);
}
