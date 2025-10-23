using BusinessObjects;
using BusinessObjects.Common.Results;
using DTOs.Clubs;
using DTOs.Communities;
using DTOs.Rooms;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Repositories.Implements;
using Repositories.Persistence;
using Repositories.Persistence.Seeding;
using Repositories.WorkSeeds.Implements;
using Repositories.WorkSeeds.Interfaces;
using Services.Implementations;
using Xunit;

namespace Services.Communities.Tests;

public sealed class CascadeSeedingGuardTests
{
    [Fact]
    public async Task CreateCommunity_ShouldNotGainSeederClubs()
    {
        await using var ctx = await CascadeTestContext.CreateAsync();
        var userId = Guid.NewGuid();
        var request = new CommunityCreateRequestDto(null, "Test Community", null, null, true);

        var created = await ctx.CommunityService.CreateCommunityAsync(request, userId);
        created.IsSuccess.Should().BeTrue();

        await ctx.Seeder.SeedAsync();

        var clubCount = await ctx.Db.Clubs
            .CountAsync(c => c.CommunityId == created.Value!.Id);
        clubCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateClub_ShouldNotGainSeederRooms()
    {
        await using var ctx = await CascadeTestContext.CreateAsync();
        var userId = Guid.NewGuid();
        var communityReq = new CommunityCreateRequestDto(null, "Club Parent", null, null, true);
        var community = await ctx.CommunityService.CreateCommunityAsync(communityReq, userId);
        community.IsSuccess.Should().BeTrue();

        var clubReq = new ClubCreateRequestDto(community.Value!.Id, "My Club", null, true);
        var club = await ctx.ClubService.CreateClubAsync(clubReq, userId);
        club.IsSuccess.Should().BeTrue();

        await ctx.Seeder.SeedAsync();

        var roomCount = await ctx.Db.Rooms
            .CountAsync(r => r.ClubId == club.Value!.Id);
        roomCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateRoom_ShouldNotGainSeederMembers()
    {
        await using var ctx = await CascadeTestContext.CreateAsync();
        var userId = Guid.NewGuid();
        var communityReq = new CommunityCreateRequestDto(null, "Room Parent", null, null, true);
        var community = await ctx.CommunityService.CreateCommunityAsync(communityReq, userId);
        community.IsSuccess.Should().BeTrue();

        var clubReq = new ClubCreateRequestDto(community.Value!.Id, "Room Club", null, true);
        var club = await ctx.ClubService.CreateClubAsync(clubReq, userId);
        club.IsSuccess.Should().BeTrue();

        var roomReq = new RoomCreateRequestDto(club.Value!.Id, "Focus Room", null, RoomJoinPolicy.Open, null, null);
        var room = await ctx.RoomService.CreateRoomAsync(roomReq, userId);
        room.IsSuccess.Should().BeTrue();

        await ctx.Seeder.SeedAsync();

        var memberCount = await ctx.Db.RoomMembers
            .CountAsync(rm => rm.RoomId == room.Value!.Id);
        memberCount.Should().Be(1);
    }

    private sealed class CascadeTestContext : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly IGenericUnitOfWork _uow;
        private readonly UserManager<User> _userManager;

        public required AppDbContext Db { get; init; }
        public required CommunityService CommunityService { get; init; }
        public required ClubService ClubService { get; init; }
        public required RoomService RoomService { get; init; }
        public required AppSeeder Seeder { get; init; }

        private CascadeTestContext(SqliteConnection connection, IGenericUnitOfWork uow, UserManager<User> userManager)
        {
            _connection = connection;
            _uow = uow;
            _userManager = userManager;
        }

        public static async Task<CascadeTestContext> CreateAsync()
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

            var communityQuery = new CommunityQueryRepository(db);
            var communityCommand = new CommunityCommandRepository(db);
            var clubQuery = new ClubQueryRepository(db);
            var clubCommand = new ClubCommandRepository(db);
            var roomQuery = new RoomQueryRepository(db);
            var roomCommand = new RoomCommandRepository(db);

            var communityService = new CommunityService(
                uow,
                communityQuery,
                communityCommand,
                clubQuery,
                clubCommand,
                roomQuery,
                roomCommand);

            var clubService = new ClubService(
                uow,
                clubQuery,
                clubCommand,
                communityQuery,
                roomQuery,
                roomCommand);

            var roomService = new RoomService(
                uow,
                roomQuery,
                roomCommand,
                clubQuery,
                clubCommand,
                communityQuery,
                communityCommand,
                new PasswordHasher<Room>(),
                NullLogger<RoomService>.Instance);

            var gameRepository = new GameRepository(db);
            var userGameRepository = new UserGameRepository(db);

            var seedOptions = Options.Create(new SeedOptions
            {
                Run = true,
                ApplyMigrations = false,
                AllowInProduction = true,
                Roles = Array.Empty<string>(),
                Admin = new SeedOptions.AdminOptions(),
                ComprehensiveSeeding = new SeedOptions.ComprehensiveOptions
                {
                    SeedSampleUsers = false,
                    SeedCommunities = false,
                    SeedClubsAndRooms = true,
                    SeedEvents = false,
                    SeedWalletsAndTransactions = false,
                    SeedFriendships = false,
                    SeedGifts = false,
                    SeedBugReports = false
                }
            });

            var userStore = new UserStore<User, Role, AppDbContext, Guid>(db);
            var userManager = new UserManager<User>(
                userStore,
                null,
                new PasswordHasher<User>(),
                Array.Empty<IUserValidator<User>>(),
                Array.Empty<IPasswordValidator<User>>(),
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null,
                NullLogger<UserManager<User>>.Instance);

            var seeder = new AppSeeder(
                db,
                gameRepository,
                userGameRepository,
                uow,
                NullLogger<AppSeeder>.Instance,
                userManager,
                seedOptions);

            return new CascadeTestContext(connection, uow, userManager)
            {
                Db = db,
                CommunityService = communityService,
                ClubService = clubService,
                RoomService = roomService,
                Seeder = seeder
            };
        }

        public async ValueTask DisposeAsync()
        {
            await _uow.DisposeAsync();
            _userManager.Dispose();
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
