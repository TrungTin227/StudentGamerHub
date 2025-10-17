using System.Linq;
using BusinessObjects;
using BusinessObjects.Common.Results;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Implements;
using Repositories.Persistence;
using Repositories.WorkSeeds.Implements;
using Repositories.WorkSeeds.Interfaces;
using Services.Implementations;

namespace Services.Events.Tests;

public sealed class RegistrationServiceTests
{
    [Fact]
    public async Task RegisterAsync_ShouldRespectCapacityAndReleaseSlotWhenCanceled()
    {
        await using var ctx = await RegistrationServiceTestContext.CreateAsync();

        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var attendees = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();

        ctx.Db.Events.Add(new Event
        {
            Id = eventId,
            OrganizerId = organizerId,
            Title = "Practice Tournament",
            Status = EventStatus.Open,
            Mode = EventMode.Online,
            PriceCents = 10_000,
            Capacity = 3,
            StartsAt = DateTime.UtcNow.AddDays(1),
            CreatedBy = organizerId,
        });

        await ctx.Db.SaveChangesAsync();

        var first = await ctx.Service.RegisterAsync(attendees[0], eventId);
        var second = await ctx.Service.RegisterAsync(attendees[1], eventId);
        var third = await ctx.Service.RegisterAsync(attendees[2], eventId);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        third.IsSuccess.Should().BeTrue();

        var blocked = await ctx.Service.RegisterAsync(attendees[3], eventId);

        blocked.IsSuccess.Should().BeFalse();
        blocked.Error.Should().NotBeNull();
        blocked.Error!.Code.Should().Be(Error.Codes.Forbidden);
        blocked.Error.Message.Should().Be("Event has reached capacity.");

        var firstRegistration = await ctx.Db.EventRegistrations
            .SingleAsync(r => r.EventId == eventId && r.UserId == attendees[0]);
        firstRegistration.Status = EventRegistrationStatus.Canceled;

        var firstIntent = await ctx.Db.PaymentIntents
            .SingleAsync(pi => pi.EventRegistrationId == firstRegistration.Id);
        firstIntent.Status = PaymentIntentStatus.Canceled;
        firstIntent.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);

        await ctx.Db.SaveChangesAsync();

        var retried = await ctx.Service.RegisterAsync(attendees[3], eventId);

        retried.IsSuccess.Should().BeTrue();
        var newIntentId = retried.Value;

        var activePending = await ctx.PaymentIntentRepository.CountActivePendingByEventAsync(
            eventId,
            DateTime.UtcNow,
            CancellationToken.None);
        activePending.Should().Be(3);

        var intents = await ctx.Db.PaymentIntents
            .Where(pi => pi.EventId == eventId)
            .ToListAsync();

        intents.Should().Contain(pi => pi.Id == newIntentId && pi.Status == PaymentIntentStatus.RequiresPayment);
        intents.Should().Contain(pi => pi.Status == PaymentIntentStatus.Canceled);

        var registrationStatuses = await ctx.Db.EventRegistrations
            .Where(r => r.EventId == eventId)
            .Select(r => r.Status)
            .ToListAsync();

        registrationStatuses.Count(status => status == EventRegistrationStatus.Pending)
            .Should().Be(3);
        registrationStatuses.Should().Contain(EventRegistrationStatus.Canceled);
    }

    private sealed class RegistrationServiceTestContext : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly IGenericUnitOfWork _uow;

        private RegistrationServiceTestContext(SqliteConnection connection, IGenericUnitOfWork uow)
        {
            _connection = connection;
            _uow = uow;
        }

        public required AppDbContext Db { get; init; }
        public required RegistrationService Service { get; init; }
        public required PaymentIntentRepository PaymentIntentRepository { get; init; }

        public static async Task<RegistrationServiceTestContext> CreateAsync()
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
            var eventQueryRepository = new EventQueryRepository(db);
            var registrationQueryRepository = new RegistrationQueryRepository(db);
            var registrationCommandRepository = new RegistrationCommandRepository(db);
            var paymentIntentRepository = new PaymentIntentRepository(db);

            var service = new RegistrationService(
                uow,
                eventQueryRepository,
                registrationQueryRepository,
                registrationCommandRepository,
                paymentIntentRepository);

            return new RegistrationServiceTestContext(connection, uow)
            {
                Db = db,
                Service = service,
                PaymentIntentRepository = paymentIntentRepository,
            };
        }

        public async ValueTask DisposeAsync()
        {
            await _uow.DisposeAsync();
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
