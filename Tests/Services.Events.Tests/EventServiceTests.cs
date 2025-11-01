using System.Linq;
using BusinessObjects;
using BusinessObjects.Common;
using DTOs.Events;
using DTOs.Events.Validation;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Repositories.Implements;
using Repositories.Persistence;
using Repositories.WorkSeeds.Implements;
using BusinessObjects.Common.Results;
using Services.Implementations;
using Xunit;

namespace Services.Events.Tests;

public sealed class EventServiceTests
{
    [Fact]
    public async Task CreateAsync_ShouldResetQuota_WhenMonthChanged()
    {
        await using var ctx = await EventServiceTestContext.CreateAsync();
        var plan = await ctx.Db.MembershipPlans.SingleAsync(p => p.Name == "Pro");

        var now = DateTime.UtcNow;
        var membership = new UserMembership
        {
            Id = Guid.NewGuid(),
            UserId = ctx.OrganizerId,
            MembershipPlanId = plan.Id,
            StartDate = now.AddMonths(-1),
            EndDate = now.AddMonths(1),
            RemainingEventQuota = 0,
            LastResetAtUtc = now.AddMonths(-1),
            CreatedAtUtc = now.AddMonths(-1),
            CreatedBy = ctx.OrganizerId,
        };

        ctx.Db.UserMemberships.Add(membership);
        await ctx.Db.SaveChangesAsync();

        var request = ctx.CreateRequest("Tournament Reset Check");

        var result = await ctx.ExecuteCreateAsync(request);

        result.IsSuccess.Should().BeTrue();

        var refreshed = await ctx.Db.UserMemberships
            .AsNoTracking()
            .SingleAsync(m => m.Id == membership.Id);

        refreshed.LastResetAtUtc.Should().NotBeNull();
        refreshed.LastResetAtUtc!.Value.Month.Should().Be(DateTime.UtcNow.Month);
        refreshed.LastResetAtUtc.Value.Year.Should().Be(DateTime.UtcNow.Year);
        refreshed.RemainingEventQuota.Should().Be(plan.MonthlyEventLimit - 1);
    }

    [Fact]
    public async Task CreateAsync_ShouldPreventDoubleQuotaConsumption_WhenCalledConcurrently()
    {
        await using var ctx = await EventServiceTestContext.CreateAsync();
        var plan = await ctx.Db.MembershipPlans.SingleAsync(p => p.Name == "Basic");

        var now = DateTime.UtcNow;
        var membership = new UserMembership
        {
            Id = Guid.NewGuid(),
            UserId = ctx.OrganizerId,
            MembershipPlanId = plan.Id,
            StartDate = now.AddDays(-10),
            EndDate = now.AddDays(10),
            RemainingEventQuota = 1,
            LastResetAtUtc = now,
            CreatedAtUtc = now.AddDays(-10),
            CreatedBy = ctx.OrganizerId,
        };

        ctx.Db.UserMemberships.Add(membership);
        await ctx.Db.SaveChangesAsync();

        var request1 = ctx.CreateRequest("Concurrent Test 1");
        var request2 = ctx.CreateRequest("Concurrent Test 2");

        var first = ctx.ExecuteCreateAsync(request1);
        var second = ctx.ExecuteCreateAsync(request2);

        var results = await Task.WhenAll(first, second);

        results.Count(r => r.IsSuccess).Should().Be(1);
        results.Count(r => r.IsFailure).Should().Be(1);
        results.Single(r => r.IsFailure).Error!.Code.Should().Be("EventLimitReachedForCurrentMembership");

        var refreshed = await ctx.Db.UserMemberships
            .AsNoTracking()
            .SingleAsync(m => m.Id == membership.Id);

        refreshed.RemainingEventQuota.Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_ShouldApplyMembershipPolicyDefaults()
    {
        await using var ctx = await EventServiceTestContext.CreateAsync();
        var plan = await ctx.Db.MembershipPlans.SingleAsync(p => p.Name == "Basic");

        var now = DateTime.UtcNow;
        var membership = new UserMembership
        {
            Id = Guid.NewGuid(),
            UserId = ctx.OrganizerId,
            MembershipPlanId = plan.Id,
            StartDate = now.AddDays(-1),
            EndDate = now.AddMonths(1),
            RemainingEventQuota = plan.MonthlyEventLimit,
            LastResetAtUtc = now,
            CreatedAtUtc = now,
            CreatedBy = ctx.OrganizerId,
        };

        ctx.Db.UserMemberships.Add(membership);
        await ctx.Db.SaveChangesAsync();

        var request = ctx.CreateRequest("Membership Policy Event");

        var result = await ctx.ExecuteCreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        var created = await ctx.Db.Events
            .AsNoTracking()
            .SingleAsync(e => e.Id == result.Value);

        created.EscrowMinCents.Should().Be(0);
        created.PlatformFeeRate.Should().Be(0.05m);
        created.GatewayFeePolicy.Should().Be(GatewayFeePolicy.OrganizerPays);
    }

    private sealed class EventServiceTestContext : IAsyncDisposable
    {
        private readonly SqliteConnection _keepAliveConnection;
        private readonly string _connectionString;
        private readonly IServiceProvider _serviceProvider;

        private EventServiceTestContext(SqliteConnection keepAliveConnection, string connectionString, AppDbContext db, Guid organizerId)
        {
            _keepAliveConnection = keepAliveConnection;
            _connectionString = connectionString;
            Db = db;
            OrganizerId = organizerId;
            _serviceProvider = new ServiceCollection().BuildServiceProvider();
        }

        public AppDbContext Db { get; }
        public Guid OrganizerId { get; }

        public static async Task<EventServiceTestContext> CreateAsync()
        {
            var connectionString = "Data Source=event_service_tests;Mode=Memory;Cache=Shared";
            var keepAliveConnection = new SqliteConnection(connectionString);
            await keepAliveConnection.OpenAsync();
            RegisterSqliteGuidFunctions(keepAliveConnection);

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(keepAliveConnection)
                .Options;

            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var organizerId = Guid.NewGuid();
            var organizer = new User
            {
                Id = organizerId,
                UserName = "organizer",
                NormalizedUserName = "ORGANIZER",
                Email = "organizer@example.com",
                NormalizedEmail = "ORGANIZER@EXAMPLE.COM",
                SecurityStamp = Guid.NewGuid().ToString()
            };

            db.Users.Add(organizer);
            await db.SaveChangesAsync();

            return new EventServiceTestContext(keepAliveConnection, connectionString, db, organizerId);
        }

        public EventCreateRequestDto CreateRequest(string title)
        {
            var now = DateTime.UtcNow;
            return new EventCreateRequestDto(
                CommunityId: null,
                Title: title,
                Description: "Test event",
                Mode: EventMode.Offline,
                Location: "Arena",
                StartsAt: now.AddDays(2),
            EndsAt: now.AddDays(2).AddHours(2),
            PriceCents: 10_000,
            Capacity: 50);
        }

        public async Task<Result<Guid>> ExecuteCreateAsync(EventCreateRequestDto request)
        {
            return await UseServiceAsync((service, _) => service.CreateAsync(OrganizerId, request));
        }

        private async Task<TResult> UseServiceAsync<TResult>(Func<EventService, AppDbContext, Task<TResult>> action)
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            RegisterSqliteGuidFunctions(connection);

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            await using var context = new AppDbContext(options);
            var factory = new RepositoryFactory(context, _serviceProvider);
            await using var uow = new UnitOfWork(context, factory);

            var service = new EventService(
                uow,
                new EventCommandRepository(context),
                new EventQueryRepository(context),
                new EscrowRepository(context),
                new RegistrationQueryRepository(context),
                new RegistrationCommandRepository(context),
                new WalletRepository(context, NullLogger<WalletRepository>.Instance),
                new TransactionRepository(context),
                new UserMembershipRepository(context),
                new EventCreateRequestDtoValidator());

            return await action(service, context).ConfigureAwait(false);
        }

        private static void RegisterSqliteGuidFunctions(SqliteConnection connection)
        {
            connection.CreateFunction<Guid, Guid, Guid>("LEAST", static (a, b) => a.CompareTo(b) <= 0 ? a : b, isDeterministic: true);
            connection.CreateFunction<Guid, Guid, Guid>("GREATEST", static (a, b) => a.CompareTo(b) >= 0 ? a : b, isDeterministic: true);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _keepAliveConnection.DisposeAsync();
        }
    }
}
