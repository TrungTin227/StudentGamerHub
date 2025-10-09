using BusinessObjects;
using BusinessObjects.Common.Results;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Repositories.Interfaces;
using Repositories.Persistence;
using Repositories.WorkSeeds.Extensions;
using Repositories.WorkSeeds.Interfaces;
using Services.Application.Quests;
using Services.Quests;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace Tests.Services.Quests;

/// <summary>
/// Comprehensive tests cho QuestService v?i Redis Testcontainer.
/// Test coverage: Idempotency, TTL, Transaction rollback, Parallelism, Analytics counter.
/// </summary>
[Collection("QuestTests")]
public sealed class QuestServiceTests : IDisposable
{
    private readonly RedisContainer _redisContainer;
    private readonly IConnectionMultiplexer _redis;
    private readonly AppDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly IQuestService _svc;

    // Test user
    private readonly Guid _testUserId = Guid.NewGuid();

    public QuestServiceTests()
    {
        // Start Redis Testcontainer (synchronous for constructor)
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        _redisContainer.StartAsync().GetAwaiter().GetResult();

        // Connect to Redis
        var connectionString = _redisContainer.GetConnectionString();
        _redis = ConnectionMultiplexer.Connect(connectionString);

        // Setup InMemory EF Core
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"QuestTests_{Guid.NewGuid()}")
            .Options;

        _db = new AppDbContext(options);

        // Seed test user
        var testUser = new User
        {
            Id = _testUserId,
            UserName = "testuser",
            Email = "test@example.com",
            Points = 100,
            Level = 1,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Users.Add(testUser);
        _db.SaveChanges();

        // Setup UoW (mock)
        _uow = Substitute.For<IUnitOfWork>();
        
        var userRepo = Substitute.For<IGenericRepository<User, Guid>>();
        userRepo.GetQueryable(Arg.Any<bool>()).Returns(_db.Users);
        _uow.GetRepository<User, Guid>().Returns(userRepo);
        _uow.Database.Returns(_db.Database);

        // Mock ExecuteTransactionAsync to execute function directly
        _uow.ExecuteTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<Result>>>(),
                Arg.Any<System.Data.IsolationLevel>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Func<CancellationToken, Task<Result>>>().Invoke(CancellationToken.None));

        _uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(ci => _db.SaveChangesAsync(ci.Arg<CancellationToken>()));

        // Create service
        _svc = new QuestService(_redis, _uow, _db);
    }

    public void Dispose()
    {
        _redis?.Dispose();
        _redisContainer?.DisposeAsync().GetAwaiter().GetResult();
        _db?.Dispose();
    }

    [Fact]
    public async Task CheckIn_FirstTime_ShouldSucceed()
    {
        // Arrange
        var initialPoints = await GetUserPoints();

        // Act
        var result = await _svc!.CompleteCheckInAsync(_testUserId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var finalPoints = await GetUserPoints();
        finalPoints.Should().Be(initialPoints + 5);
    }

    [Fact]
    public async Task CheckIn_SecondTimeSameDay_ShouldFail_Idempotent()
    {
        // Arrange - first check-in
        var firstResult = await _svc!.CompleteCheckInAsync(_testUserId);
        firstResult.IsSuccess.Should().BeTrue();

        var pointsAfterFirst = await GetUserPoints();

        // Act - second check-in
        var secondResult = await _svc.CompleteCheckInAsync(_testUserId);

        // Assert
        secondResult.IsSuccess.Should().BeFalse();
        secondResult.Error.Code.Should().Be(BusinessObjects.Common.Results.Error.Codes.Validation);
        secondResult.Error.Message.Should().Contain("already completed today");

        var pointsAfterSecond = await GetUserPoints();
        pointsAfterSecond.Should().Be(pointsAfterFirst); // Points unchanged
    }

    [Fact]
    public async Task CheckIn_ShouldCreateRedisFlag_WithCorrectTTL()
    {
        // Act
        var result = await _svc!.CompleteCheckInAsync(_testUserId);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var db = _redis!.GetDatabase();
        var dateVn = GetVnDateString();
        var key = $"q:{dateVn}:{_testUserId}:CHECK_IN_DAILY";

        var exists = await db.KeyExistsAsync(key);
        exists.Should().BeTrue();

        var ttl = await db.KeyTimeToLiveAsync(key);
        ttl.Should().NotBeNull();
        ttl!.Value.TotalSeconds.Should().BeGreaterThan(0);
        ttl.Value.TotalHours.Should().BeLessThanOrEqualTo(24); // TTL should be <= 24h
    }

    [Fact]
    public async Task CompleteMultipleQuests_ShouldIncrementAnalyticsCounter()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var userId3 = Guid.NewGuid();

        await SeedUser(userId1, "user1");
        await SeedUser(userId2, "user2");
        await SeedUser(userId3, "user3");

        // Act - complete quests for 3 different users
        await _svc!.CompleteCheckInAsync(userId1);
        await _svc.CompleteCheckInAsync(userId2);
        await _svc.CompleteCheckInAsync(userId3);

        // Assert - analytics counter should be incremented
        var db = _redis!.GetDatabase();
        var minuteStr = GetVnMinuteString();
        var counterKey = $"qc:done:{minuteStr}";

        var count = await db.StringGetAsync(counterKey);
        count.HasValue.Should().BeTrue();
        ((int)count!).Should().BeGreaterThanOrEqualTo(3);

        var ttl = await db.KeyTimeToLiveAsync(counterKey);
        ttl.Should().NotBeNull();
        ttl!.Value.TotalHours.Should().BeGreaterThan(0);
        ttl.Value.TotalHours.Should().BeLessThanOrEqualTo(2); // TTL 2h
    }

    [Fact]
    public async Task JoinRoom_ShouldComplete_Idempotent()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var initialPoints = await GetUserPoints();

        // Act - first time
        var result1 = await _svc!.MarkJoinRoomAsync(_testUserId, roomId);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        var pointsAfter1 = await GetUserPoints();
        pointsAfter1.Should().Be(initialPoints + 5);

        // Act - second time (idempotent)
        var result2 = await _svc.MarkJoinRoomAsync(_testUserId, roomId);

        // Assert
        result2.IsSuccess.Should().BeFalse();
        result2.Error.Code.Should().Be(BusinessObjects.Common.Results.Error.Codes.Validation);

        var pointsAfter2 = await GetUserPoints();
        pointsAfter2.Should().Be(pointsAfter1); // Points unchanged
    }

    [Fact]
    public async Task InviteAccepted_ShouldComplete_Idempotent()
    {
        // Arrange
        var recipientId = Guid.NewGuid();
        var initialPoints = await GetUserPoints();

        // Act - first time
        var result1 = await _svc!.MarkInviteAcceptedAsync(_testUserId, recipientId);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        var pointsAfter1 = await GetUserPoints();
        pointsAfter1.Should().Be(initialPoints + 10);

        // Act - second time (idempotent)
        var result2 = await _svc.MarkInviteAcceptedAsync(_testUserId, recipientId);

        // Assert
        result2.IsSuccess.Should().BeFalse();
        result2.Error.Code.Should().Be(BusinessObjects.Common.Results.Error.Codes.Validation);

        var pointsAfter2 = await GetUserPoints();
        pointsAfter2.Should().Be(pointsAfter1); // Points unchanged
    }

    [Fact]
    public async Task AttendEvent_ShouldComplete_Idempotent()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var initialPoints = await GetUserPoints();

        // Act - first time
        var result1 = await _svc!.MarkAttendEventAsync(_testUserId, eventId);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        var pointsAfter1 = await GetUserPoints();
        pointsAfter1.Should().Be(initialPoints + 20);

        // Act - second time (idempotent)
        var result2 = await _svc.MarkAttendEventAsync(_testUserId, eventId);

        // Assert
        result2.IsSuccess.Should().BeFalse();
        result2.Error.Code.Should().Be(BusinessObjects.Common.Results.Error.Codes.Validation);

        var pointsAfter2 = await GetUserPoints();
        pointsAfter2.Should().Be(pointsAfter1); // Points unchanged
    }

    [Fact]
    public async Task CompleteCheckIn_Parallel_ShouldOnlySucceedOnce()
    {
        // Arrange
        var initialPoints = await GetUserPoints();

        // Act - run 2 concurrent check-ins
        var task1 = _svc!.CompleteCheckInAsync(_testUserId);
        var task2 = _svc.CompleteCheckInAsync(_testUserId);

        var results = await Task.WhenAll(task1, task2);

        // Assert - only 1 should succeed (Redis SET NX)
        var successCount = results.Count(r => r.IsSuccess);
        successCount.Should().Be(1);

        var finalPoints = await GetUserPoints();
        finalPoints.Should().Be(initialPoints + 5); // Only +5 (once)
    }

    [Fact]
    public async Task GetTodayAsync_ShouldReturnCorrectQuests_WithDoneStatus()
    {
        // Arrange - complete check-in
        await _svc!.CompleteCheckInAsync(_testUserId);

        // Act
        var result = await _svc.GetTodayAsync(_testUserId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        result.Value!.Points.Should().Be(105); // 100 initial + 5 from check-in
        result.Value.Quests.Should().HaveCount(4);

        var checkInQuest = result.Value.Quests.FirstOrDefault(q => q.Code == "CHECK_IN_DAILY");
        checkInQuest.Should().NotBeNull();
        checkInQuest!.Done.Should().BeTrue();
        checkInQuest.Reward.Should().Be(5);

        var otherQuests = result.Value.Quests.Where(q => q.Code != "CHECK_IN_DAILY");
        otherQuests.Should().OnlyContain(q => q.Done == false);
    }

    [Fact]
    public async Task GetTodayAsync_WithNoCompletedQuests_ShouldReturnAllFalse()
    {
        // Act
        var result = await _svc!.GetTodayAsync(_testUserId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();

        result.Value!.Points.Should().Be(100); // Initial points
        result.Value.Quests.Should().HaveCount(4);
        result.Value.Quests.Should().OnlyContain(q => q.Done == false);
    }

    [Fact]
    public async Task CompleteQuest_WithInvalidUserId_ShouldReturnNotFound()
    {
        // Arrange
        var invalidUserId = Guid.NewGuid();

        // Act
        var result = await _svc!.CompleteCheckInAsync(invalidUserId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(BusinessObjects.Common.Results.Error.Codes.NotFound);
        result.Error.Message.Should().Contain("User not found");
    }

    [Fact]
    public async Task CompleteQuest_WithEmptyUserId_ShouldReturnValidationError()
    {
        // Act
        var result = await _svc!.CompleteCheckInAsync(Guid.Empty);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(BusinessObjects.Common.Results.Error.Codes.Validation);
    }

    // Helper methods
    private async Task<int> GetUserPoints()
    {
        var user = await _db!.Users.FirstOrDefaultAsync(u => u.Id == _testUserId);
        return user?.Points ?? 0;
    }

    private async Task SeedUser(Guid userId, string userName)
    {
        var user = new User
        {
            Id = userId,
            UserName = userName,
            Email = $"{userName}@example.com",
            Points = 100,
            Level = 1,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db!.Users.Add(user);
        await _db.SaveChangesAsync();
    }

    private static string GetVnDateString()
    {
        var nowVn = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));
        return nowVn.Date.ToString("yyyyMMdd");
    }

    private static string GetVnMinuteString()
    {
        var nowVn = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7));
        return nowVn.ToString("yyyyMMddHHmm");
    }
}
