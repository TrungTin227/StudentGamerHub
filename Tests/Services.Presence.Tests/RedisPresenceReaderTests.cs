using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;
using Xunit;
using System.Linq;

namespace Services.Presence.Tests;

public sealed class RedisPresenceReaderTests
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
    private readonly Dictionary<Guid, double> _scores = new();
    private readonly Dictionary<Guid, string> _lastSeen = new();
    private readonly RedisPresenceReader _reader;

    public RedisPresenceReaderTests()
    {
        _connection.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(_database);
        _database.CreateBatch(Arg.Any<object>()).Returns(_batch);

        _batch.When(b => b.Execute()).Do(_ => { });

        _batch.SortedSetScoreAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<RedisValue>(),
                Arg.Any<CommandFlags>())
            .Returns(ci =>
            {
                var member = ci.Arg<RedisValue>().ToString();
                var id = Guid.Parse(member);
                return Task.FromResult(_scores.TryGetValue(id, out var score) ? (double?)score : null);
            });

        _batch.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(ci =>
            {
                var key = ci.Arg<RedisKey>().ToString();
                var idPart = key.Split(':').Last();
                if (Guid.TryParse(idPart, out var id) && _lastSeen.TryGetValue(id, out var value))
                {
                    return Task.FromResult((RedisValue)value);
                }

                return Task.FromResult(RedisValue.Null);
            });

        _database.SortedSetRemoveRangeByScoreAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<Exclude>(),
                Arg.Any<CommandFlags>())
            .Returns(ci =>
            {
                var min = ci.Arg<double>();
                var max = ci.Arg<double>();
                var removed = _scores
                    .Where(kvp => kvp.Value >= min && kvp.Value <= max)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var id in removed)
                {
                    _scores.Remove(id);
                }

                return Task.FromResult((long)removed.Count);
            });

        _database.SortedSetRangeByScoreWithScoresAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<Exclude>(),
                Arg.Any<Order>(),
                Arg.Any<long>(),
                Arg.Any<long>(),
                Arg.Any<CommandFlags>())
            .Returns(ci =>
            {
                var min = ci.Arg<double>();
                var take = ci.Arg<long>(6);
                if (take <= 0)
                {
                    take = _scores.Count;
                }

                var entries = _scores
                    .Where(kvp => kvp.Value >= min)
                    .OrderBy(kvp => kvp.Value)
                    .ThenBy(kvp => kvp.Key)
                    .Take((int)take)
                    .Select(kvp => new SortedSetEntry(kvp.Key.ToString("D"), kvp.Value))
                    .ToArray();

                return Task.FromResult(entries);
            });

        _database.SortedSetLengthAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<Exclude>(),
                Arg.Any<CommandFlags>())
            .Returns(ci =>
            {
                var min = ci.Arg<double>();
                var count = _scores.Values.Count(score => score >= min);
                return Task.FromResult((long)count);
            });

        _reader = new RedisPresenceReader(_connection, Options.Create(_options));
    }

    [Fact]
    public async Task GetBatchAsync_ShouldReturnSnapshotsWithOnlineStatus()
    {
        var now = DateTimeOffset.UtcNow;
        var online = Guid.NewGuid();
        var offline = Guid.NewGuid();
        _scores[online] = now.AddSeconds(30).ToUnixTimeMilliseconds();
        _lastSeen[online] = now.ToString("O");
        _lastSeen[offline] = now.AddMinutes(-5).ToString("O");

        var result = await _reader.GetBatchAsync(new[] { online, offline }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        var onlineSnapshot = result.Value.Single(x => x.UserId == online);
        onlineSnapshot.IsOnline.Should().BeTrue();
        onlineSnapshot.TtlRemainingSeconds.Should().NotBeNull();
        onlineSnapshot.LastSeenUtc.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));

        var offlineSnapshot = result.Value.Single(x => x.UserId == offline);
        offlineSnapshot.IsOnline.Should().BeFalse();
        offlineSnapshot.TtlRemainingSeconds.Should().BeNull();
        offlineSnapshot.LastSeenUtc.Should().BeCloseTo(now.AddMinutes(-5), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetOnlineAsync_ShouldReturnPagedResultsWithCursor()
    {
        var now = DateTimeOffset.UtcNow;
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        var expired = Guid.NewGuid();

        _scores[first] = now.AddSeconds(20).ToUnixTimeMilliseconds();
        _scores[second] = now.AddSeconds(40).ToUnixTimeMilliseconds();
        _scores[third] = now.AddSeconds(60).ToUnixTimeMilliseconds();
        _scores[expired] = now.AddSeconds(-_options.GraceSeconds - 10).ToUnixTimeMilliseconds();

        _lastSeen[first] = now.ToString("O");
        _lastSeen[second] = now.ToString("O");
        _lastSeen[third] = now.ToString("O");
        _lastSeen[expired] = now.ToString("O");

        var firstPage = await _reader.GetOnlineAsync(new PresenceOnlineQuery(2, null), CancellationToken.None);

        firstPage.IsSuccess.Should().BeTrue();
        firstPage.Value.Items.Should().HaveCount(2);
        firstPage.Value.NextCursor.Should().NotBeNull();
        _scores.ContainsKey(expired).Should().BeFalse("expired entries should be cleaned");

        var secondPage = await _reader.GetOnlineAsync(new PresenceOnlineQuery(2, firstPage.Value.NextCursor), CancellationToken.None);

        secondPage.IsSuccess.Should().BeTrue();
        secondPage.Value.Items.Should().ContainSingle(x => x.UserId == third);
        secondPage.Value.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldReturnGlobalCounts()
    {
        var now = DateTimeOffset.UtcNow;
        var online = Guid.NewGuid();
        var offline = Guid.NewGuid();
        _scores[online] = now.AddSeconds(45).ToUnixTimeMilliseconds();
        _scores[offline] = now.AddSeconds(-_options.GraceSeconds - 20).ToUnixTimeMilliseconds();

        var result = await _reader.GetSummaryAsync(new PresenceSummaryRequest(null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Online.Should().Be(1);
        result.Value.Offline.Should().BeNull();
        result.Value.Total.Should().Be(1);
        result.Value.Scope.Should().Be("global");

        var scoped = await _reader.GetSummaryAsync(new PresenceSummaryRequest(new[] { online, offline }), CancellationToken.None);

        scoped.IsSuccess.Should().BeTrue();
        scoped.Value.Online.Should().Be(1);
        scoped.Value.Offline.Should().Be(1);
        scoped.Value.Total.Should().Be(2);
        scoped.Value.Scope.Should().Be("batch");
    }
}
