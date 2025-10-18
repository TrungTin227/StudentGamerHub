using Application.Friends;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using BusinessObjects.Common.Results;

namespace Services.Presence;

public sealed class PresenceService : IPresenceService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly PresenceOptions _options;
    private readonly string _prefix;

    public PresenceService(IConnectionMultiplexer redis, IOptions<PresenceOptions> options)
    {
        _redis = redis;
        _options = options.Value;
        _prefix = PresenceKeyHelper.NormalizePrefix(_options.KeyPrefix);
    }

    public async Task<Result> HeartbeatAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            return Result.Failure(new Error(Error.Codes.Validation, "User id is required"));
        }

        var result = await ResultExtensions
            .TryAsync(async () =>
            {
                ct.ThrowIfCancellationRequested();

                var now = DateTimeOffset.UtcNow;
                var ttl = TimeSpan.FromSeconds(_options.TtlSeconds);
                var expiry = now.Add(ttl);
                var db = _redis.GetDatabase();

                var batch = db.CreateBatch();
                var presenceTask = batch.StringSetAsync(
                    PresenceKeyHelper.GetPresenceKey(_prefix, userId),
                    "1",
                    ttl);
                var lastSeenTask = batch.StringSetAsync(
                    PresenceKeyHelper.GetLastSeenKey(_prefix, userId),
                    now.ToString("O"));
                var indexTask = batch.SortedSetAddAsync(
                    PresenceKeyHelper.GetIndexKey(_prefix),
                    PresenceKeyHelper.GetMember(userId),
                    expiry.ToUnixTimeMilliseconds());
                var cleanupThreshold = now.AddSeconds(-_options.GraceSeconds).ToUnixTimeMilliseconds() - 1;
                var cleanupTask = batch.SortedSetRemoveRangeByScoreAsync(
                    PresenceKeyHelper.GetIndexKey(_prefix),
                    double.NegativeInfinity,
                    cleanupThreshold);

                batch.Execute();
                await Task
                    .WhenAll(presenceTask, lastSeenTask, indexTask, cleanupTask)
                    .ConfigureAwait(false);

                return true;
            })
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            return Result.Success();
        }
        return Result.Failure(result.Error);
    }

    public Task<Result<bool>> IsOnlineAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            return Task.FromResult(Result<bool>.Failure(new Error(Error.Codes.Validation, "User id is required")));
        }

        return ResultExtensions.TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();

            var db = _redis.GetDatabase();
            var now = DateTimeOffset.UtcNow;
            var indexKey = PresenceKeyHelper.GetIndexKey(_prefix);
            var member = PresenceKeyHelper.GetMember(userId);
            var score = await db.SortedSetScoreAsync(indexKey, member).ConfigureAwait(false);
            if (!score.HasValue)
            {
                return false;
            }

            var expiry = DateTimeOffset.FromUnixTimeMilliseconds(ToMilliseconds(score.Value));
            if (expiry < now.AddSeconds(-_options.GraceSeconds))
            {
                await db.SortedSetRemoveAsync(indexKey, member).ConfigureAwait(false);
                return false;
            }

            return true;
        });
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

        return ResultExtensions.TryAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();

            var distinctIds = userIds.Where(id => id != Guid.Empty).Distinct().ToArray();
            if (distinctIds.Length == 0)
            {
                return (IReadOnlyDictionary<Guid, bool>)new Dictionary<Guid, bool>();
            }

            var db = _redis.GetDatabase();
            var batch = db.CreateBatch();
            var tasks = new Dictionary<Guid, Task<double?>>(distinctIds.Length);
            var indexKey = PresenceKeyHelper.GetIndexKey(_prefix);
            foreach (var id in distinctIds)
            {
                tasks[id] = batch.SortedSetScoreAsync(indexKey, PresenceKeyHelper.GetMember(id));
            }

            batch.Execute();
            await Task.WhenAll(tasks.Values).ConfigureAwait(false);

            var threshold = DateTimeOffset.UtcNow.AddSeconds(-_options.GraceSeconds);
            var result = tasks.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Result.HasValue &&
                       DateTimeOffset.FromUnixTimeMilliseconds(ToMilliseconds(kvp.Value.Result!.Value)) >= threshold);

            return (IReadOnlyDictionary<Guid, bool>)result;
        });
    }

    private static long ToMilliseconds(double score) => (long)Math.Round(score, MidpointRounding.AwayFromZero);
}
