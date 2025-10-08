using Application.Friends;
using BusinessObjects.Common.Results;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Services.Presence;
using StackExchange.Redis;

namespace Services.Presence.Tests;

public sealed class PresenceServiceTests
{
    private readonly IConnectionMultiplexer _connection = Substitute.For<IConnectionMultiplexer>();
    private readonly IDatabase _database = Substitute.For<IDatabase>();
    private readonly IBatch _batch = Substitute.For<IBatch>();
    private readonly PresenceOptions _options = new() { TtlSeconds = 60, HeartbeatSeconds = 30 };
    private readonly Dictionary<string, CacheEntry> _store = new(StringComparer.Ordinal);
    private readonly PresenceService _service;

    public PresenceServiceTests()
    {
        _connection.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);
        _database.CreateBatch(Arg.Any<object>()).Returns(_batch);

        _database.StringSetAsync(
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
            _store[key] = new CacheEntry(value, expiry.HasValue ? DateTimeOffset.UtcNow.Add(expiry.Value) : null);
            return Task.FromResult(true);
        });

        _database.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(ci => Task.FromResult(IsAlive(ci.Arg<RedisKey>().ToString())));

        _batch.KeyExistsAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(ci => Task.FromResult(IsAlive(ci.Arg<RedisKey>().ToString())));

        _batch.ExecuteAsync(Arg.Any<CommandFlags>()).Returns(Task.CompletedTask);

        _service = new PresenceService(_connection, Options.Create(_options));
    }

    [Fact]
    public async Task HeartbeatAsync_ShouldSetPresenceKeyWithTtl()
    {
        var userId = Guid.NewGuid();

        var result = await _service.HeartbeatAsync(userId);

        result.IsSuccess.Should().BeTrue();
        _store.TryGetValue($"presence:{userId}", out var entry).Should().BeTrue();
        entry!.ExpiresAt.Should().NotBeNull();
        entry.ExpiresAt!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow.AddSeconds(_options.TtlSeconds), TimeSpan.FromSeconds(2));
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

        var key = $"presence:{userId}";
        _store[key] = _store[key] with { ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1) };

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

        return entry.ExpiresAt > DateTimeOffset.UtcNow;
    }

    private sealed record CacheEntry(string Value, DateTimeOffset? ExpiresAt);
}
