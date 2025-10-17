using BusinessObjects.Common.Results;
using BusinessObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StackExchange.Redis;
using System.Collections.Concurrent;
using Xunit;
using Repositories.Implements;
using Repositories.Persistence;
using Repositories.WorkSeeds.Implements;
using Repositories.WorkSeeds.Interfaces;
using Services.Implementations;

namespace Services.Games.Tests;

public sealed class GamesServiceTests
{
    [Fact]
    public async Task CreateAsync_ShouldReturnValidation_WhenNameEmpty()
    {
        await using var ctx = await GamesTestContext.CreateAsync();

        var result = await ctx.CatalogService.CreateAsync(string.Empty);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Validation);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnConflict_WhenNameExists()
    {
        await using var ctx = await GamesTestContext.CreateAsync();
        ctx.Db.Games.Add(new Game { Id = Guid.NewGuid(), Name = "Valorant" });
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.CatalogService.CreateAsync("valorant");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Conflict);
    }

    [Fact]
    public async Task RenameAsync_ShouldReturnConflict_WhenTargetNameExists()
    {
        await using var ctx = await GamesTestContext.CreateAsync();
        var gameA = new Game { Id = Guid.NewGuid(), Name = "Valorant" };
        var gameB = new Game { Id = Guid.NewGuid(), Name = "Apex Legends" };
        ctx.Db.Games.AddRange(gameA, gameB);
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.CatalogService.RenameAsync(gameB.Id, "Valorant");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Conflict);
    }

    [Fact]
    public async Task AddAsync_ShouldReturnNotFound_WhenGameMissing()
    {
        await using var ctx = await GamesTestContext.CreateAsync();
        var userId = Guid.NewGuid();

        var result = await ctx.UserGameService.AddAsync(userId, Guid.NewGuid(), null, null);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.NotFound);
    }

    [Fact]
    public async Task AddAsync_ShouldReturnConflict_WhenAlreadyExists()
    {
        await using var ctx = await GamesTestContext.CreateAsync();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        ctx.Db.Games.Add(new Game { Id = gameId, Name = "Valorant" });
        ctx.Db.UserGames.Add(new UserGame { UserId = userId, GameId = gameId });
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.UserGameService.AddAsync(userId, gameId, null, null);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Conflict);
    }

    [Fact]
    public async Task AddAsync_ShouldReturnValidation_WhenLimitExceeded()
    {
        await using var ctx = await GamesTestContext.CreateAsync();
        var userId = Guid.NewGuid();

        for (var i = 0; i < 20; i++)
        {
            var game = new Game { Id = Guid.NewGuid(), Name = $"Game {i}" };
            ctx.Db.Games.Add(game);
            ctx.Db.UserGames.Add(new UserGame { UserId = userId, GameId = game.Id });
        }
        await ctx.Db.SaveChangesAsync();

        var extraGameId = Guid.NewGuid();
        ctx.Db.Games.Add(new Game { Id = extraGameId, Name = "Extra" });
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.UserGameService.AddAsync(userId, extraGameId, null, null);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Validation);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnValidation_WhenInGameNameTooLong()
    {
        await using var ctx = await GamesTestContext.CreateAsync();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        ctx.Db.Games.Add(new Game { Id = gameId, Name = "Valorant" });
        ctx.Db.UserGames.Add(new UserGame { UserId = userId, GameId = gameId });
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.UserGameService.UpdateAsync(userId, gameId, new string('a', 65), null);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Validation);
    }

    [Fact]
    public async Task RemoveAsync_ShouldBeIdempotent()
    {
        await using var ctx = await GamesTestContext.CreateAsync();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        ctx.Db.Games.Add(new Game { Id = gameId, Name = "Valorant" });
        ctx.Db.UserGames.Add(new UserGame { UserId = userId, GameId = gameId });
        await ctx.Db.SaveChangesAsync();

        var first = await ctx.UserGameService.RemoveAsync(userId, gameId);
        var second = await ctx.UserGameService.RemoveAsync(userId, gameId);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
    }

    private sealed class GamesTestContext : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, (RedisValue Value, DateTime? Expiration)> _cache = new();

        public required AppDbContext Db { get; init; }
        public required IGenericUnitOfWork Uow { get; init; }
        public required GameCatalogService CatalogService { get; init; }
        public required UserGameService UserGameService { get; init; }

        private GamesTestContext() { }

        public static async Task<GamesTestContext> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"GamesTests-{Guid.NewGuid()}")
                .Options;

            var db = new TestAppDbContext(options);
            // InMemory provider doesn't require EnsureCreated for basic CRUD in tests

            var services = new ServiceCollection().BuildServiceProvider();
            var factory = new RepositoryFactory(db, services);
            var uow = new UnitOfWork(db, factory);
            var gameRepo = new GameRepository(db);
            var userGameRepo = new UserGameRepository(db);

            var redis = Substitute.For<IConnectionMultiplexer>();
            var database = Substitute.For<IDatabase>();
            var catalogService = new GameCatalogService(uow, gameRepo, redis);
            var userGameService = new UserGameService(uow, userGameRepo, gameRepo, redis);

            var ctx = new GamesTestContext()
            {
                Db = db,
                Uow = uow,
                CatalogService = catalogService,
                UserGameService = userGameService,
            };

            ConfigureRedis(redis, database, ctx._cache);

            return ctx;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await Uow.DisposeAsync();
        }

        private static void ConfigureRedis(IConnectionMultiplexer redis, IDatabase database, ConcurrentDictionary<string, (RedisValue Value, DateTime? Expiration)> cache)
        {
            redis.GetDatabase(Arg.Any<int>(), Arg.Any<object?>()).Returns(database);

            database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(callInfo =>
            {
                var key = callInfo.Arg<RedisKey>().ToString();
                if (key is null)
                {
                    return Task.FromResult(RedisValue.Null);
                }

                if (cache.TryGetValue(key, out var entry))
                {
                    if (entry.Expiration is { } expiry && expiry <= DateTime.UtcNow)
                    {
                        cache.TryRemove(key, out _);
                        return Task.FromResult(RedisValue.Null);
                    }

                    return Task.FromResult(entry.Value);
                }

                return Task.FromResult(RedisValue.Null);
            });

            database.StringSetAsync(Arg.Any<RedisKey>(), Arg.Any<RedisValue>(), Arg.Any<TimeSpan?>(), Arg.Any<When>(), Arg.Any<CommandFlags>())
                .Returns(callInfo =>
                {
                    var key = callInfo.Arg<RedisKey>().ToString();
                    if (key is null)
                    {
                        return Task.FromResult(false);
                    }

                    var value = callInfo.Arg<RedisValue>();
                    var ttl = callInfo.Arg<TimeSpan?>();
                    DateTime? expiration = ttl.HasValue ? DateTime.UtcNow.Add(ttl.Value) : null;
                    cache[key] = (value, expiration);
                    return Task.FromResult(true);
                });

            database.KeyDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>()).Returns(callInfo =>
            {
                var key = callInfo.Arg<RedisKey>().ToString();
                if (key is null)
                {
                    return Task.FromResult(false);
                }

                return Task.FromResult(cache.TryRemove(key, out _));
            });
        }

        // Minimal test DbContext to avoid provider-specific configuration in AppDbContext
        private sealed class TestAppDbContext : AppDbContext
        {
            public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

            protected override void OnModelCreating(ModelBuilder b)
            {
                // Only map the entities used by these tests
                b.Entity<Game>(e =>
                {
                    e.ToTable("games");
                    e.HasKey(x => x.Id);
                    e.Property(x => x.Name).IsRequired().HasMaxLength(128);
                });

                b.Entity<UserGame>(e =>
                {
                    e.ToTable("user_games");
                    e.HasKey(x => new { x.UserId, x.GameId });
                    e.Property(x => x.InGameName).HasMaxLength(64);
                });
            }
        }
    }
}
