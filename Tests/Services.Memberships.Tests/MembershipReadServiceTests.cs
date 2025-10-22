using BusinessObjects;
using BusinessObjects.Common.Enums;
using BusinessObjects.Common.Results;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Services.DTOs.Memberships;
using Services.Implementations.Memberships;
using Xunit;

namespace Services.Memberships.Tests;

public sealed class MembershipReadServiceTests
{
    [Fact]
    public async Task GetMyMembershipTreeAsync_ShouldReturnHierarchyForJoinedEntities()
    {
        await using var ctx = await MembershipReadServiceTestContext.CreateAsync();
        var userId = Guid.NewGuid();
        ctx.Db.Users.Add(CreateUser(userId));

        var alphaCommunity = CreateCommunity("Alpha Community", userId);
        var betaCommunity = CreateCommunity("Beta Community", userId);
        ctx.Db.Communities.AddRange(alphaCommunity, betaCommunity);

        var arcadeClub = CreateClub(alphaCommunity.Id, "Arcade Club", userId);
        var boardClub = CreateClub(alphaCommunity.Id, "Board Club", userId);
        var betaClub = CreateClub(betaCommunity.Id, "Beta Club", userId);
        ctx.Db.Clubs.AddRange(arcadeClub, boardClub, betaClub);

        var arcadeRoom = CreateRoom(arcadeClub.Id, "Arcade Room", userId);
        var arcadeRoomPending = CreateRoom(arcadeClub.Id, "Arcade Pending", userId);
        var boardRoom = CreateRoom(boardClub.Id, "Board Room", userId);
        ctx.Db.Rooms.AddRange(arcadeRoom, arcadeRoomPending, boardRoom);

        ctx.Db.CommunityMembers.AddRange(
            CreateCommunityMember(alphaCommunity.Id, userId),
            CreateCommunityMember(betaCommunity.Id, userId));

        ctx.Db.ClubMembers.AddRange(
            CreateClubMember(arcadeClub.Id, userId),
            CreateClubMember(boardClub.Id, userId));

        ctx.Db.RoomMembers.AddRange(
            CreateRoomMember(arcadeRoom.Id, userId, RoomMemberStatus.Approved),
            CreateRoomMember(arcadeRoomPending.Id, userId, RoomMemberStatus.Pending));

        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.GetMyMembershipTreeAsync(userId);

        result.IsSuccess.Should().BeTrue();
        var tree = result.Value.Should().NotBeNull().Subject;

        tree.Overview.CommunityCount.Should().Be(2);
        tree.Overview.ClubCount.Should().Be(2);
        tree.Overview.RoomCount.Should().Be(1);

        tree.Communities.Should().HaveCount(2);
        tree.Communities.Select(c => c.CommunityName)
            .Should().ContainInOrder("Alpha Community", "Beta Community");

        var alphaNode = tree.Communities.First();
        alphaNode.Clubs.Should().HaveCount(2);
        alphaNode.Clubs.Select(c => c.ClubName)
            .Should().ContainInOrder("Arcade Club", "Board Club");

        var arcadeNode = alphaNode.Clubs.First();
        arcadeNode.Rooms.Should().ContainSingle();
        arcadeNode.Rooms[0].RoomName.Should().Be("Arcade Room");

        var boardNode = alphaNode.Clubs.Skip(1).First();
        boardNode.Rooms.Should().BeEmpty();

        var betaNode = tree.Communities.Last();
        betaNode.Clubs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMyMembershipTreeAsync_ShouldReturnEmptyTree_WhenUserHasNoMemberships()
    {
        await using var ctx = await MembershipReadServiceTestContext.CreateAsync();
        var userId = Guid.NewGuid();
        ctx.Db.Users.Add(CreateUser(userId));
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.GetMyMembershipTreeAsync(userId);

        result.IsSuccess.Should().BeTrue();
        var tree = result.Value.Should().NotBeNull().Subject;
        tree.Communities.Should().BeEmpty();
        tree.Overview.CommunityCount.Should().Be(0);
        tree.Overview.ClubCount.Should().Be(0);
        tree.Overview.RoomCount.Should().Be(0);
    }

    [Fact]
    public async Task GetMyMembershipTreeAsync_ShouldFail_WhenUserIdIsEmpty()
    {
        await using var ctx = await MembershipReadServiceTestContext.CreateAsync();

        var result = await ctx.Service.GetMyMembershipTreeAsync(Guid.Empty);

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

    private static Community CreateCommunity(string name, Guid createdBy) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = null,
        IsPublic = true,
        MembersCount = 0,
        CreatedAtUtc = DateTime.UtcNow,
        CreatedBy = createdBy
    };

    private static Club CreateClub(Guid communityId, string name, Guid createdBy) => new()
    {
        Id = Guid.NewGuid(),
        CommunityId = communityId,
        Name = name,
        Description = null,
        IsPublic = true,
        MembersCount = 0,
        CreatedAtUtc = DateTime.UtcNow,
        CreatedBy = createdBy
    };

    private static Room CreateRoom(Guid clubId, string name, Guid createdBy) => new()
    {
        Id = Guid.NewGuid(),
        ClubId = clubId,
        Name = name,
        Description = null,
        JoinPolicy = RoomJoinPolicy.Open,
        MembersCount = 0,
        CreatedAtUtc = DateTime.UtcNow,
        CreatedBy = createdBy
    };

    private static CommunityMember CreateCommunityMember(Guid communityId, Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        CommunityId = communityId,
        UserId = userId,
        Role = MemberRole.Member,
        JoinedAt = DateTime.UtcNow,
        CreatedAtUtc = DateTime.UtcNow,
        CreatedBy = userId
    };

    private static ClubMember CreateClubMember(Guid clubId, Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        ClubId = clubId,
        UserId = userId,
        Role = MemberRole.Member,
        JoinedAt = DateTime.UtcNow,
        CreatedAtUtc = DateTime.UtcNow,
        CreatedBy = userId
    };

    private static RoomMember CreateRoomMember(Guid roomId, Guid userId, RoomMemberStatus status) => new()
    {
        Id = Guid.NewGuid(),
        RoomId = roomId,
        UserId = userId,
        Role = RoomRole.Member,
        Status = status,
        JoinedAt = DateTime.UtcNow,
        CreatedAtUtc = DateTime.UtcNow,
        CreatedBy = userId
    };

    private sealed class MembershipReadServiceTestContext : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        public required AppDbContext Db { get; init; }
        public required MembershipReadService Service { get; init; }

        private MembershipReadServiceTestContext(SqliteConnection connection)
        {
            _connection = connection;
        }

        public static async Task<MembershipReadServiceTestContext> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var service = new MembershipReadService(db);

            return new MembershipReadServiceTestContext(connection)
            {
                Db = db,
                Service = service
            };
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
