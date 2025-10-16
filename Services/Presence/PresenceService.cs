using Application.Friends;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using BusinessObjects.Common.Results;

namespace Services.Presence;

public sealed class PresenceService : IPresenceService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly PresenceOptions _options;

    public PresenceService(IConnectionMultiplexer redis, IOptions<PresenceOptions> options)
    {
        _redis = redis;
        _options = options.Value;
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

                var ttl = TimeSpan.FromSeconds(_options.TtlSeconds);
                var timestamp = DateTime.UtcNow.ToString("O");
                var db = _redis.GetDatabase();

                await Task
                    .WhenAll(
                        db.StringSetAsync(GetPresenceKey(userId), "1", ttl),
                        db.StringSetAsync(GetLastSeenKey(userId), timestamp))
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
            var key = GetPresenceKey(userId);
            var exists = await db.KeyExistsAsync(key).ConfigureAwait(false);
            return exists;
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
            var tasks = new Dictionary<Guid, Task<bool>>(distinctIds.Length);

            foreach (var id in distinctIds)
            {
                var key = GetPresenceKey(id);
                tasks[id] = batch.KeyExistsAsync(key);
            }

            batch.Execute();
            await Task.WhenAll(tasks.Values).ConfigureAwait(false);

            var result = tasks.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Result);
            return (IReadOnlyDictionary<Guid, bool>)result;
        });
    }

    private static RedisKey GetPresenceKey(Guid userId) => $"presence:{userId}";
    private static RedisKey GetLastSeenKey(Guid userId) => $"lastseen:{userId}";
}
