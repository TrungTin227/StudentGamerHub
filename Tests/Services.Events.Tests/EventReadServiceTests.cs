using System.Linq;
using BusinessObjects;
using BusinessObjects.Common;
using BusinessObjects.Common.Results;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Repositories.Implements;
using Repositories.Interfaces;
using Repositories.Persistence;
using Services.Implementations;
using Services.Interfaces;
using Xunit;

namespace Services.Events.Tests;

public sealed class EventReadServiceTests
{
    [Fact]
    public async Task GetById_ShouldReturnEvent_WhenActive()
    {
        await using var ctx = await EventTestContext.CreateAsync();
        var organizerId = Guid.NewGuid();
        var attendeeId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        ctx.Db.Users.AddRange(
            CreateUser(organizerId, "organizer"),
            CreateUser(attendeeId, "attendee"));

        var ev = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = "LAN Party",
            Description = "Bring your laptop",
            Mode = EventMode.Offline,
            StartsAt = now.AddDays(2),
            EndsAt = now.AddDays(2).AddHours(4),
            Status = EventStatus.Open,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = organizerId
        };
        ctx.Db.Events.Add(ev);

        ctx.Db.Escrows.Add(new Escrow
        {
            Id = Guid.NewGuid(),
            EventId = ev.Id,
            AmountHoldCents = 150_000,
            Status = EscrowStatus.Held,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = organizerId
        });

        ctx.Db.EventRegistrations.Add(new EventRegistration
        {
            Id = Guid.NewGuid(),
            EventId = ev.Id,
            UserId = attendeeId,
            Status = EventRegistrationStatus.Confirmed,
            RegisteredAt = now,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = attendeeId
        });

        await ctx.Db.SaveChangesAsync();

        var result = await ctx.EventReadService.GetByIdAsync(attendeeId, ev.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(ev.Id);
        result.Value.Title.Should().Be("LAN Party");
        result.Value.MyRegistrationStatus.Should().Be(EventRegistrationStatus.Confirmed);
        result.Value.IsOrganizer.Should().BeFalse();
    }

    [Fact]
    public async Task GetById_ShouldReturnNotFound_WhenSoftDeleted()
    {
        await using var ctx = await EventTestContext.CreateAsync();
        var organizerId = Guid.NewGuid();

        ctx.Db.Users.Add(CreateUser(organizerId, "deleted-organizer"));

        var ev = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = "Deleted Event",
            Status = EventStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = organizerId,
            IsDeleted = true,
            DeletedAtUtc = DateTime.UtcNow
        };
        ctx.Db.Events.Add(ev);
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.EventReadService.GetByIdAsync(Guid.NewGuid(), ev.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be(Error.Codes.NotFound);
    }

    [Fact]
    public async Task SearchAsync_ShouldRespectFilters()
    {
        await using var ctx = await EventTestContext.CreateAsync();
        var organizerId = Guid.NewGuid();
        var communityId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        ctx.Db.Users.Add(CreateUser(organizerId, "search-organizer"));
        ctx.Db.Communities.Add(new Community
        {
            Id = communityId,
            Name = "LAN Hub",
            Description = "Test community",
            IsPublic = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = organizerId
        });

        var matching = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            CommunityId = communityId,
            Title = "LAN Marathon",
            Description = "Weekend LAN party",
            Status = EventStatus.Open,
            StartsAt = now.AddDays(1),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = organizerId
        };
        ctx.Db.Events.Add(matching);

        var differentStatus = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            CommunityId = communityId,
            Title = "LAN Closed",
            Status = EventStatus.Completed,
            StartsAt = now.AddDays(1),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = organizerId
        };
        ctx.Db.Events.Add(differentStatus);

        var outsideWindow = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            CommunityId = communityId,
            Title = "LAN Future",
            Status = EventStatus.Open,
            StartsAt = now.AddDays(10),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = organizerId
        };
        ctx.Db.Events.Add(outsideWindow);

        await ctx.Db.SaveChangesAsync();

        var result = await ctx.EventReadService.SearchAsync(
            userId,
            new[] { EventStatus.Open },
            communityId,
            organizerId,
            now,
            now.AddDays(2),
            "LAN",
            sortAscByStartsAt: true,
            page: 1,
            pageSize: 10);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].Id.Should().Be(matching.Id);
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task ListForEvent_ShouldAllowOrganizer_AndRejectOthers()
    {
        await using var ctx = await EventTestContext.CreateAsync();
        var organizerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var attendeeId = Guid.NewGuid();

        ctx.Db.Users.AddRange(
            CreateUser(organizerId, "list-organizer"),
            CreateUser(attendeeId, "list-attendee"));

        ctx.Db.Events.Add(new Event
        {
            Id = eventId,
            OrganizerId = organizerId,
            Title = "Organizer View",
            Status = EventStatus.Open,
            StartsAt = DateTime.UtcNow.AddDays(1),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = organizerId
        });

        ctx.Db.EventRegistrations.Add(new EventRegistration
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = attendeeId,
            Status = EventRegistrationStatus.Pending,
            RegisteredAt = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = attendeeId
        });

        await ctx.Db.SaveChangesAsync();

        var success = await ctx.RegistrationReadService.ListForEventAsync(organizerId, eventId, null, 1, 10);
        success.IsSuccess.Should().BeTrue();
        success.Value!.Items.Should().HaveCount(1);
        success.Value.Items[0].UserId.Should().Be(attendeeId);

        var forbidden = await ctx.RegistrationReadService.ListForEventAsync(Guid.NewGuid(), eventId, null, 1, 10);
        forbidden.IsSuccess.Should().BeFalse();
        forbidden.Error!.Code.Should().Be(Error.Codes.Forbidden);
    }

    [Fact]
    public async Task ListMine_ShouldReturnOnlyCurrentUserRegistrations()
    {
        await using var ctx = await EventTestContext.CreateAsync();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var event1 = Guid.NewGuid();
        var event2 = Guid.NewGuid();
        var organizerAId = Guid.NewGuid();
        var organizerBId = Guid.NewGuid();

        ctx.Db.Users.AddRange(
            CreateUser(userId, "mine-user"),
            CreateUser(otherUserId, "mine-other"),
            CreateUser(organizerAId, "mine-org-a"),
            CreateUser(organizerBId, "mine-org-b"));

        ctx.Db.Events.AddRange(
            new Event
            {
                Id = event1,
                OrganizerId = organizerAId,
                Title = "Event A",
                Status = EventStatus.Open,
                StartsAt = DateTime.UtcNow.AddDays(1),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = organizerAId
            },
            new Event
            {
                Id = event2,
                OrganizerId = organizerBId,
                Title = "Event B",
                Status = EventStatus.Open,
                StartsAt = DateTime.UtcNow.AddDays(2),
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = organizerBId
            });

        ctx.Db.EventRegistrations.AddRange(
            new EventRegistration
            {
                Id = Guid.NewGuid(),
                EventId = event1,
                UserId = userId,
                Status = EventRegistrationStatus.Confirmed,
                RegisteredAt = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = userId
            },
            new EventRegistration
            {
                Id = Guid.NewGuid(),
                EventId = event2,
                UserId = userId,
                Status = EventRegistrationStatus.Pending,
                RegisteredAt = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = userId
            },
            new EventRegistration
            {
                Id = Guid.NewGuid(),
                EventId = event2,
                UserId = otherUserId,
                Status = EventRegistrationStatus.Pending,
                RegisteredAt = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = otherUserId
            });

        await ctx.Db.SaveChangesAsync();

        var result = await ctx.RegistrationReadService.ListMineAsync(userId, null, 1, 10);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items.Should().OnlyContain(r => r.EventId == event1 || r.EventId == event2);
        result.Value.Items.Select(r => r.EventTitle).Should().BeEquivalentTo(new[] { "Event A", "Event B" });
    }

    [Fact]
    public async Task PaymentRead_ShouldReturnIntent_ForOwnerOnly()
    {
        await using var ctx = await EventTestContext.CreateAsync();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        ctx.Db.Users.AddRange(
            CreateUser(ownerId, "payment-owner"),
            CreateUser(otherUserId, "payment-other"));

        var intent = new PaymentIntent
        {
            Id = Guid.NewGuid(),
            UserId = ownerId,
            AmountCents = 123_000,
            Purpose = PaymentPurpose.EventTicket,
            Status = PaymentIntentStatus.RequiresPayment,
            ClientSecret = "secret",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = ownerId
        };
        ctx.Db.PaymentIntents.Add(intent);
        await ctx.Db.SaveChangesAsync();

        var success = await ctx.PaymentReadService.GetAsync(ownerId, intent.Id);
        success.IsSuccess.Should().BeTrue();
        success.Value!.Id.Should().Be(intent.Id);

        var failure = await ctx.PaymentReadService.GetAsync(otherUserId, intent.Id);
        failure.IsSuccess.Should().BeFalse();
        failure.Error!.Code.Should().Be(Error.Codes.NotFound);
    }

    private static User CreateUser(Guid id, string name)
    {
        var normalizedUserName = name.ToUpperInvariant();
        var email = $"{name}@example.com";

        return new User
        {
            Id = id,
            UserName = name,
            NormalizedUserName = normalizedUserName,
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString()
        };
    }

    private sealed class EventTestContext : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private EventTestContext(
            SqliteConnection connection,
            AppDbContext db,
            IEventReadService eventReadService,
            IRegistrationReadService registrationReadService,
            IPaymentReadService paymentReadService)
        {
            _connection = connection;
            Db = db;
            EventReadService = eventReadService;
            RegistrationReadService = registrationReadService;
            PaymentReadService = paymentReadService;
        }

        public AppDbContext Db { get; }
        public IEventReadService EventReadService { get; }
        public IRegistrationReadService RegistrationReadService { get; }
        public IPaymentReadService PaymentReadService { get; }

        public static async Task<EventTestContext> CreateAsync()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            await connection.OpenAsync();
            RegisterSqliteGuidFunctions(connection);

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            IEventQueryRepository eventQueryRepository = new EventQueryRepository(db);
            IRegistrationQueryRepository registrationQueryRepository = new RegistrationQueryRepository(db);
            var escrowRepository = new EscrowRepository(db);
            var paymentIntentRepository = new PaymentIntentRepository(db);
            var transactionRepository = new TransactionRepository(db);

            var eventReadService = new EventReadService(eventQueryRepository, escrowRepository, registrationQueryRepository);
            var registrationReadService = new RegistrationReadService(eventQueryRepository, registrationQueryRepository);
            var paymentReadService = new PaymentReadService(paymentIntentRepository, transactionRepository);

            return new EventTestContext(
                connection,
                db,
                eventReadService,
                registrationReadService,
                paymentReadService);
        }

        private static void RegisterSqliteGuidFunctions(SqliteConnection connection)
        {
            connection.CreateFunction<Guid, Guid, Guid>("LEAST", static (a, b) => a.CompareTo(b) <= 0 ? a : b, isDeterministic: true);
            connection.CreateFunction<Guid, Guid, Guid>("GREATEST", static (a, b) => a.CompareTo(b) >= 0 ? a : b, isDeterministic: true);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
