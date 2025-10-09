using Application.Friends;
using BusinessObjects;
using BusinessObjects.Common;
using BusinessObjects.Common.Results;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Persistence;
using Repositories.WorkSeeds.Implements;
using Repositories.WorkSeeds.Interfaces;
using Services.Friends;
using Xunit;

namespace Services.Presence.Tests;

public sealed class FriendServiceTests
{
    [Fact]
    public async Task InviteAsync_ShouldRejectSelfInvitation()
    {
        await using var ctx = await FriendServiceTestContext.CreateAsync();
        var user = CreateUser();
        ctx.Db.Users.Add(user);
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.InviteAsync(user.Id, user.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Validation);
    }

    [Fact]
    public async Task InviteAsync_ShouldFailWhenTargetMissing()
    {
        await using var ctx = await FriendServiceTestContext.CreateAsync();
        var requester = CreateUser();
        ctx.Db.Users.Add(requester);
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.InviteAsync(requester.Id, Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.NotFound);
    }

    [Fact]
    public async Task InviteAsync_ShouldFailWhenAlreadyFriends()
    {
        await using var ctx = await FriendServiceTestContext.CreateAsync();
        var requester = CreateUser();
        var target = CreateUser();
        ctx.Db.Users.AddRange(requester, target);
        ctx.Db.FriendLinks.Add(new FriendLink
        {
            Id = Guid.NewGuid(),
            SenderId = requester.Id,
            RecipientId = target.Id,
            Status = FriendStatus.Accepted,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
            CreatedBy = requester.Id,
            RespondedAt = DateTimeOffset.UtcNow.AddDays(-2),
        });
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.InviteAsync(requester.Id, target.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Conflict);
    }

    [Fact]
    public async Task InviteAsync_ShouldFailWhenPendingSentByRequester()
    {
        await using var ctx = await FriendServiceTestContext.CreateAsync();
        var requester = CreateUser();
        var target = CreateUser();
        ctx.Db.Users.AddRange(requester, target);
        ctx.Db.FriendLinks.Add(new FriendLink
        {
            Id = Guid.NewGuid(),
            SenderId = requester.Id,
            RecipientId = target.Id,
            Status = FriendStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            CreatedBy = requester.Id,
        });
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.InviteAsync(requester.Id, target.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Conflict);
    }

    [Fact]
    public async Task InviteAsync_ShouldNotifyWhenPendingFromOtherSide()
    {
        await using var ctx = await FriendServiceTestContext.CreateAsync();
        var requester = CreateUser();
        var target = CreateUser();
        ctx.Db.Users.AddRange(requester, target);
        ctx.Db.FriendLinks.Add(new FriendLink
        {
            Id = Guid.NewGuid(),
            SenderId = target.Id,
            RecipientId = requester.Id,
            Status = FriendStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            CreatedBy = target.Id,
        });
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.InviteAsync(requester.Id, target.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Conflict);
        result.Error.Message.Should().Contain("Đối phương đã mời bạn trước");
    }

    [Fact]
    public async Task InviteAsync_ShouldEnforceDeclinedCooldown()
    {
        await using var ctx = await FriendServiceTestContext.CreateAsync();
        var requester = CreateUser();
        var target = CreateUser();
        ctx.Db.Users.AddRange(requester, target);
        ctx.Db.FriendLinks.Add(new FriendLink
        {
            Id = Guid.NewGuid(),
            SenderId = requester.Id,
            RecipientId = target.Id,
            Status = FriendStatus.Declined,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
            CreatedBy = requester.Id,
            RespondedAt = DateTimeOffset.UtcNow.AddHours(-2),
        });
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.InviteAsync(requester.Id, target.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Forbidden);
    }

    [Fact]
    public async Task InviteAsync_ShouldResetDeclinedAfterCooldown()
    {
        await using var ctx = await FriendServiceTestContext.CreateAsync();
        var requester = CreateUser();
        var target = CreateUser();
        ctx.Db.Users.AddRange(requester, target);
        ctx.Db.FriendLinks.Add(new FriendLink
        {
            Id = Guid.NewGuid(),
            SenderId = requester.Id,
            RecipientId = target.Id,
            Status = FriendStatus.Declined,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-5),
            CreatedBy = requester.Id,
            RespondedAt = DateTimeOffset.UtcNow.AddDays(-2),
        });
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.InviteAsync(requester.Id, target.Id);

        result.IsSuccess.Should().BeTrue();
        var link = await ctx.Db.FriendLinks.SingleAsync();
        link.Status.Should().Be(FriendStatus.Pending);
        link.SenderId.Should().Be(requester.Id);
        link.RecipientId.Should().Be(target.Id);
        link.RespondedAt.Should().BeNull();
    }

    [Fact]
    public async Task AcceptAsync_ShouldRejectWhenCurrentUserNotRecipient()
    {
        await using var ctx = await FriendServiceTestContext.CreateAsync();
        var requester = CreateUser();
        var target = CreateUser();
        ctx.Db.Users.AddRange(requester, target);
        ctx.Db.FriendLinks.Add(new FriendLink
        {
            Id = Guid.NewGuid(),
            SenderId = requester.Id,
            RecipientId = target.Id,
            Status = FriendStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = requester.Id,
        });
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.AcceptAsync(requester.Id, target.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Forbidden);
    }

    [Fact]
    public async Task AcceptAsync_ShouldSucceedForRecipient()
    {
        await using var ctx = await FriendServiceTestContext.CreateAsync();
        var requester = CreateUser();
        var target = CreateUser();
        ctx.Db.Users.AddRange(requester, target);
        ctx.Db.FriendLinks.Add(new FriendLink
        {
            Id = Guid.NewGuid(),
            SenderId = target.Id,
            RecipientId = requester.Id,
            Status = FriendStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = target.Id,
        });
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.AcceptAsync(requester.Id, target.Id);

        result.IsSuccess.Should().BeTrue();
        var link = await ctx.Db.FriendLinks.SingleAsync();
        link.Status.Should().Be(FriendStatus.Accepted);
        link.RespondedAt.Should().NotBeNull();
    }

    private static User CreateUser()
    {
        var id = Guid.NewGuid();
        var userName = $"user_{id:N}";
        return new User
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = $"{userName}@example.com",
            NormalizedEmail = $"{userName}@example.com".ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    private sealed class FriendServiceTestContext : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly IGenericUnitOfWork _uow;

        public required AppDbContext Db { get; init; }
        public required FriendService Service { get; init; }

        private FriendServiceTestContext(SqliteConnection connection, IGenericUnitOfWork uow)
        {
            _connection = connection;
            _uow = uow;
        }

        public static async Task<FriendServiceTestContext> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var services = new ServiceCollection().BuildServiceProvider();
            var factory = new RepositoryFactory(db, services);
            var uow = new UnitOfWork(db, factory);
            var service = new FriendService(uow);

            return new FriendServiceTestContext(connection, uow)
            {
                Db = db,
                Service = service,
            };
        }

        public async ValueTask DisposeAsync()
        {
            await _uow.DisposeAsync();
            await Db.DisposeAsync();
            await connectionDisposeAsync();
        }

        private ValueTask connectionDisposeAsync()
        {
            _connection.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
