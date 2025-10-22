using BusinessObjects;
using BusinessObjects.Common.Enums;
using BusinessObjects.Common.Results;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Services.DTOs.Memberships;
using Services.Implementations;
using Services.Implementations.Memberships;
using Xunit;

namespace Services.Memberships.Tests;

public sealed class MembershipReadServiceTests
{
    [Fact]
    public async Task JoinClubAsync_ShouldSucceedWithoutCommunityMembership()
    {
        await using var ctx = await MembershipScenarioTestContext.CreateAsync();
        var userId = Guid.NewGuid();
        var community = CreateCommunity();
        var club = CreateClub(community.Id);

        ctx.Db.Users.Add(CreateUser(userId));
        ctx.Db.Communities.Add(community);
        ctx.Db.Clubs.Add(club);
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.ClubService.JoinClubAsync(club.Id, userId);

        result.IsSuccess.Should().BeTrue();
        var memberships = await ctx.Db.ClubMembers.Where(m => m.UserId == userId).ToListAsync();
        memberships.Should().ContainSingle(m => m.ClubId == club.Id);

        var communityMemberships = await ctx.Db.CommunityMembers.CountAsync();
        communityMemberships.Should().Be(0);
    }

    [Fact]
    public async Task JoinRoomAsync_ShouldFailWithoutClubMembership()
    {
        await using var ctx = await MembershipScenarioTestContext.CreateAsync();
        var userId = Guid.NewGuid();
        var community = CreateCommunity();
        var club = CreateClub(community.Id);
        var room = CreateRoom(club.Id, RoomJoinPolicy.Open);

        ctx.Db.Users.Add(CreateUser(userId));
        ctx.Db.Communities.Add(community);
        ctx.Db.Clubs.Add(club);
        ctx.Db.Rooms.Add(room);
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.RoomService.JoinRoomAsync(room.Id, userId, null);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be(Error.Codes.Conflict);
        result.Error.Message.Should().Be("ClubMembershipRequired");
    }

    [Fact]
    public async Task GetMyClubRoomTreeAsync_ShouldReturnClubWithoutRooms_WhenOnlyClubJoined()
    {
        await using var ctx = await MembershipScenarioTestContext.CreateAsync();
        var userId = Guid.NewGuid();
        var community = CreateCommunity();
        var club = CreateClub(community.Id);

        ctx.Db.Users.Add(CreateUser(userId));
        ctx.Db.Communities.Add(community);
        ctx.Db.Clubs.Add(club);
        await ctx.Db.SaveChangesAsync();

        var joinResult = await ctx.ClubService.JoinClubAsync(club.Id, userId);
        joinResult.IsSuccess.Should().BeTrue();

        var result = await ctx.MembershipReadService.GetMyClubRoomTreeAsync(userId);

        result.IsSuccess.Should().BeTrue();
        var tree = result.Value.Should().NotBeNull().Subject;
        tree.Clubs.Should().ContainSingle();
        var node = tree.Clubs[0];
        node.ClubId.Should().Be(club.Id);
        node.Rooms.Should().BeEmpty();
        tree.Overview.ClubCount.Should().Be(1);
        tree.Overview.RoomCount.Should().Be(0);
    }

    [Fact]
    public async Task GetMyClubRoomTreeAsync_ShouldIncludeJoinedRoomsAndOverviewCounts()
    {
        await using var ctx = await MembershipScenarioTestContext.CreateAsync();
        var userId = Guid.NewGuid();
        var community = CreateCommunity();
        var club = CreateClub(community.Id);
        var room = CreateRoom(club.Id, RoomJoinPolicy.Open);

        ctx.Db.Users.Add(CreateUser(userId));
        ctx.Db.Communities.Add(community);
        ctx.Db.Clubs.Add(club);
        ctx.Db.Rooms.Add(room);
        await ctx.Db.SaveChangesAsync();

        var joinClub = await ctx.ClubService.JoinClubAsync(club.Id, userId);
        joinClub.IsSuccess.Should().BeTrue();

        var joinRoom = await ctx.RoomService.JoinRoomAsync(room.Id, userId, null);
        joinRoom.IsSuccess.Should().BeTrue();

        var result = await ctx.MembershipReadService.GetMyClubRoomTreeAsync(userId);

        result.IsSuccess.Should().BeTrue();
        var tree = result.Value.Should().NotBeNull().Subject;
        tree.Clubs.Should().ContainSingle();
        var node = tree.Clubs[0];
        node.Rooms.Should().ContainSingle();
        node.Rooms[0].RoomId.Should().Be(room.Id);
        tree.Overview.ClubCount.Should().Be(1);
        tree.Overview.RoomCount.Should().Be(1);
    }

    [Fact]
    public async Task GetMyClubRoomTreeAsync_ShouldExcludeSoftDeletedAndBannedMemberships()
    {
        await using var ctx = await MembershipScenarioTestContext.CreateAsync();
        var userId = Guid.NewGuid();
        var community = CreateCommunity();
        var activeClub = CreateClub(community.Id, "Active Club");
        var deletedClub = CreateClub(community.Id, "Deleted Club");
        var activeRoom = CreateRoom(activeClub.Id, RoomJoinPolicy.Open, "Active Room");
        var bannedRoom = CreateRoom(activeClub.Id, RoomJoinPolicy.Open, "Banned Room");

        ctx.Db.Users.Add(CreateUser(userId));
        ctx.Db.Communities.Add(community);
        ctx.Db.Clubs.AddRange(activeClub, deletedClub);
        ctx.Db.Rooms.AddRange(activeRoom, bannedRoom);

        var now = DateTime.UtcNow;
        ctx.Db.ClubMembers.AddRange(
            new ClubMember
            {
                ClubId = activeClub.Id,
                UserId = userId,
                Role = MemberRole.Member,
                JoinedAt = now,
                CreatedAtUtc = now,
                CreatedBy = userId
            },
            new ClubMember
            {
                ClubId = deletedClub.Id,
                UserId = userId,
                Role = MemberRole.Member,
                JoinedAt = now,
                CreatedAtUtc = now,
                CreatedBy = userId,
                IsDeleted = true,
                DeletedAtUtc = now,
                DeletedBy = userId
            });

        ctx.Db.RoomMembers.AddRange(
            new RoomMember
            {
                RoomId = activeRoom.Id,
                UserId = userId,
                Role = RoomRole.Member,
                Status = RoomMemberStatus.Approved,
                JoinedAt = now,
                CreatedAtUtc = now,
                CreatedBy = userId
            },
            new RoomMember
            {
                RoomId = bannedRoom.Id,
                UserId = userId,
                Role = RoomRole.Member,
                Status = RoomMemberStatus.Banned,
                JoinedAt = now,
                CreatedAtUtc = now,
                CreatedBy = userId
            });

        await ctx.Db.SaveChangesAsync();

        var result = await ctx.MembershipReadService.GetMyClubRoomTreeAsync(userId);

        result.IsSuccess.Should().BeTrue();
        var tree = result.Value.Should().NotBeNull().Subject;
        tree.Clubs.Should().ContainSingle();
        tree.Clubs[0].ClubId.Should().Be(activeClub.Id);
        tree.Clubs[0].Rooms.Should().ContainSingle(r => r.RoomId == activeRoom.Id);
        tree.Overview.ClubCount.Should().Be(1);
        tree.Overview.RoomCount.Should().Be(1);
    }

    [Fact]
    public async Task GetMyClubRoomTreeAsync_ShouldFail_WhenUserIdIsEmpty()
    {
        await using var ctx = await MembershipScenarioTestContext.CreateAsync();

        var result = await ctx.MembershipReadService.GetMyClubRoomTreeAsync(Guid.Empty);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be(Error.Codes.Validation);
    }

    private static User CreateUser(Guid id) => new()
    {
        Id = id,
        UserName = $"user-{id}",
        NormalizedUserName = $"USER-{id}",
        Email = $"{id}@example.com",
        NormalizedEmail = $"{id}@EXAMPLE.COM",
        SecurityStamp = Guid.NewGuid().ToString(),
        ConcurrencyStamp = Guid.NewGuid().ToString(),
        CreatedAtUtc = DateTime.UtcNow
    };

    private static Community CreateCommunity() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Community",
        Description = null,
        IsPublic = true,
        MembersCount = 0,
        CreatedAtUtc = DateTime.UtcNow,
        CreatedBy = Guid.NewGuid()
    };

    private static Club CreateClub(Guid communityId, string? name = null) => new()
    {
        Id = Guid.NewGuid(),
        CommunityId = communityId,
        Name = name ?? "Club",
        Description = null,
        IsPublic = true,
        MembersCount = 0,
        CreatedAtUtc = DateTime.UtcNow,
        CreatedBy = Guid.NewGuid()
    };

    private static Room CreateRoom(Guid clubId, RoomJoinPolicy policy, string? name = null) => new()
    {
        Id = Guid.NewGuid(),
        ClubId = clubId,
        Name = name ?? "Room",
        Description = null,
        JoinPolicy = policy,
        MembersCount = 0,
        CreatedAtUtc = DateTime.UtcNow,
        CreatedBy = Guid.NewGuid()
    };

    private sealed class MembershipScenarioTestContext : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly IGenericUnitOfWork _uow;

        public required AppDbContext Db { get; init; }
        public required MembershipReadService MembershipReadService { get; init; }
        public required ClubService ClubService { get; init; }
        public required RoomService RoomService { get; init; }

        private MembershipScenarioTestContext(SqliteConnection connection, IGenericUnitOfWork uow)
        {
            _connection = connection;
            _uow = uow;
        }

        public static async Task<MembershipScenarioTestContext> CreateAsync()
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
            var clubQuery = new ClubQueryRepository(db);
            var clubCommand = new ClubCommandRepository(db);
            var roomQuery = new RoomQueryRepository(db);
            var roomCommand = new RoomCommandRepository(db);
            var clubService = new ClubService(uow, clubQuery, clubCommand, communityQuery, roomQuery, roomCommand);
            var roomService = new RoomService(uow, roomQuery, roomCommand, clubQuery, new PasswordHasher<Room>());
            var membershipReadService = new MembershipReadService(db);

            return new MembershipScenarioTestContext(connection, uow)
            {
                Db = db,
                MembershipReadService = membershipReadService,
                ClubService = clubService,
                RoomService = roomService
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
