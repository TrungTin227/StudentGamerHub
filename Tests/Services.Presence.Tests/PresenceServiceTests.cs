using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;
using Xunit;
using System.Linq;

namespace Services.Presence.Tests;

public sealed class PresenceServiceTests
{
    private readonly IConnectionMultiplexer _connection = Substitute.For<IConnectionMultiplexer>();
    private readonly IDatabase _database = Substitute.For<IDatabase>();
    private readonly IBatch _batch = Substitute.For<IBatch>();
    private readonly PresenceOptions _options = new()
    {
        TtlSeconds = 60,
        HeartbeatSeconds = 30,
        KeyPrefix = "sg",
        GraceSeconds = 5,
        MaxBatchSize = 200,
        DefaultPageSize = 100,
        MaxPageSize = 500
    };
    private readonly Dictionary<string, CacheEntry> _store = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, double>> _sortedSets = new(StringComparer.Ordinal);
    private readonly PresenceService _service;

    public PresenceServiceTests()
    {
        _connection.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);
        _database.CreateBatch(Arg.Any<object>()).Returns(_batch);

        _batch.StringSetAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<RedisValue>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>())
        .Returns(ci =>
        {
            var key = ci.Arg<RedisKey>().ToString();
            var expiry = ci.Arg<TimeSpan?>();
            var value = ci.Arg<RedisValue>().ToString();
            _store[key] = new CacheEntry(value, expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : null);
            return Task.FromResult(true);
        });

        _batch.SortedSetAddAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<RedisValue>(),
                Arg.Any<double>(),
                Arg.Any<When>(),
                Arg.Any<CommandFlags>())
            .Returns(ci =>
            {
                var key = ci.Arg<RedisKey>().ToString();
                var member = ci.Arg<RedisValue>().ToString();
                var score = ci.Arg<double>();
                if (!_sortedSets.TryGetValue(key, out var set))
                {
                    set = new Dictionary<string, double>(StringComparer.Ordinal);
                    _sortedSets[key] = set;
                }

                set[member] = score;
                return Task.FromResult(true);
            });

        _batch.SortedSetRemoveRangeByScoreAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<Exclude>(),
                Arg.Any<CommandFlags>())
            .Returns(ci =>
            {
                var key = ci.Arg<RedisKey>().ToString();
                var min = ci.Arg<double>();
                var max = ci.Arg<double>();
                if (!_sortedSets.TryGetValue(key, out var set))
                {
                    return Task.FromResult(0L);
                }

                var removed = set
                    .Where(kvp => kvp.Value >= min && kvp.Value <= max)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var member in removed)
                {
                    set.Remove(member);
                }

                return Task.FromResult((long)removed.Count);
            });

        _database.SortedSetScoreAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<RedisValue>(),
                Arg.Any<CommandFlags>())
            .Returns(ci =>
            {
                var key = ci.Arg<RedisKey>().ToString();
                var member = ci.Arg<RedisValue>().ToString();
                if (_sortedSets.TryGetValue(key, out var set) && set.TryGetValue(member, out var score))
                {
                    return Task.FromResult<double?>(score);
                }

                return Task.FromResult<double?>(null);
            });

        _batch.SortedSetScoreAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<RedisValue>(),
                Arg.Any<CommandFlags>())
            .Returns(ci => _database.SortedSetScoreAsync(ci.Arg<RedisKey>(), ci.Arg<RedisValue>(), ci.Arg<CommandFlags>()));

        // PresenceService uses IBatch.Execute(); KeyExistsAsync enqueues operations and Execute() flushes them.
        _batch.When(b => b.Execute()).Do(_ => { /* no-op for substitute */ });

        _service = new PresenceService(_connection, Options.Create(_options));
    }

    [Fact]
    public async Task HeartbeatAsync_ShouldSetPresenceKeyWithTtl()
    {
        var userId = Guid.NewGuid();

        var result = await _service.HeartbeatAsync(userId);

        result.IsSuccess.Should().BeTrue();
        _store.TryGetValue($"sg:presence:{userId}", out var entry).Should().BeTrue();
        entry!.ExpiresAt.Should().NotBeNull();
        entry.ExpiresAt!.Value.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(_options.TtlSeconds), TimeSpan.FromSeconds(2));

        _sortedSets.TryGetValue("sg:presence:index", out var set).Should().BeTrue();
        set!.ContainsKey(userId.ToString("D")).Should().BeTrue();
    }

    [Fact]
    public async Task BatchIsOnlineAsync_ShouldReturnOnlineAndOfflineUsers()
    {
        var onlineUsers = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var offlineUsers = new[] { Guid.NewGuid(), Guid.NewGuid() };

        foreach (var user in onlineUsers)
        {
            await _service.HeartbeatAsync(user);
        }

        var users = onlineUsers.Concat(offlineUsers).ToArray();

        var result = await _service.BatchIsOnlineAsync(users);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        foreach (var user in onlineUsers)
        {
            result.Value![user].Should().BeTrue();
        }

        foreach (var user in offlineUsers)
        {
            result.Value![user].Should().BeFalse();
        }
    }

    [Fact]
    public async Task IsOnlineAsync_ShouldReturnFalseAfterTtlExpires()
    {
        var userId = Guid.NewGuid();
        await _service.HeartbeatAsync(userId);

        var key = $"sg:presence:{userId}";
        _store[key] = _store[key] with { ExpiresAt = DateTime.UtcNow.AddSeconds(-1) };
        _sortedSets["sg:presence:index"][userId.ToString("D")] =
            DateTimeOffset.UtcNow.AddSeconds(-_options.GraceSeconds - 5).ToUnixTimeMilliseconds();

        var result = await _service.IsOnlineAsync(userId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    private bool IsAlive(string key)
    {
        if (!_store.TryGetValue(key, out var entry))
        {
            return false;
        }

        if (entry.ExpiresAt is null)
        {
            return true;
        }

        return entry.ExpiresAt > DateTime.UtcNow;
    }

    private sealed record CacheEntry(string Value, DateTime? ExpiresAt);
}
