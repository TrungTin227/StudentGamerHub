using BusinessObjects;
using BusinessObjects.Common;
using BusinessObjects.Common.Results;
using DTOs.Rooms;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Repositories.Implements;
using Repositories.Persistence;
using Repositories.WorkSeeds.Implements;
using Repositories.WorkSeeds.Interfaces;
using Services.Implementations;
using Xunit;

namespace Services.Rooms.Tests;

public sealed class RoomServiceTests
{
    [Fact]
    public async Task UpdateRoomAsync_ShouldRequirePassword_WhenPolicyRequiresPassword()
    {
        await using var ctx = await RoomServiceTestContext.CreateAsync();
        var ownerId = Guid.NewGuid();

        var community = SeedCommunity(ctx, ownerId);
        var club = SeedClub(ctx, community.Id, ownerId);
        var room = SeedRoom(ctx, club.Id, ownerId, RoomJoinPolicy.Open, membersCount: 1);
        SeedMember(ctx, room.Id, ownerId, RoomRole.Owner, RoomMemberStatus.Approved);
        await ctx.Db.SaveChangesAsync();

        var request = new RoomUpdateRequestDto("Updated", null, RoomJoinPolicy.RequiresPassword, null, null);

        var result = await ctx.Service.UpdateRoomAsync(ownerId, room.Id, request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Validation);
    }

    [Fact]
    public async Task UpdateRoomAsync_ShouldReturnConflict_WhenCapacityLowerThanApproved()
    {
        await using var ctx = await RoomServiceTestContext.CreateAsync();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var community = SeedCommunity(ctx, ownerId);
        var club = SeedClub(ctx, community.Id, ownerId);
        var room = SeedRoom(ctx, club.Id, ownerId, RoomJoinPolicy.Open, membersCount: 2);
        SeedMember(ctx, room.Id, ownerId, RoomRole.Owner, RoomMemberStatus.Approved);
        SeedMember(ctx, room.Id, memberId, RoomRole.Member, RoomMemberStatus.Approved);
        await ctx.Db.SaveChangesAsync();

        var request = new RoomUpdateRequestDto("Updated", "Desc", RoomJoinPolicy.Open, null, 1);

        var result = await ctx.Service.UpdateRoomAsync(ownerId, room.Id, request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Conflict);
    }

    [Fact]
    public async Task TransferOwnershipAsync_ShouldSwapRolesBetweenOwnerAndTarget()
    {
        await using var ctx = await RoomServiceTestContext.CreateAsync();
        var ownerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var community = SeedCommunity(ctx, ownerId);
        var club = SeedClub(ctx, community.Id, ownerId);
        var room = SeedRoom(ctx, club.Id, ownerId, RoomJoinPolicy.Open, membersCount: 2);
        SeedMember(ctx, room.Id, ownerId, RoomRole.Owner, RoomMemberStatus.Approved);
        SeedMember(ctx, room.Id, targetId, RoomRole.Member, RoomMemberStatus.Approved);
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.TransferOwnershipAsync(ownerId, room.Id, targetId);

        result.IsSuccess.Should().BeTrue();

        var members = await ctx.Db.RoomMembers
            .Where(m => m.RoomId == room.Id)
            .ToDictionaryAsync(m => m.UserId);

        members[ownerId].Role.Should().Be(RoomRole.Moderator);
        members[targetId].Role.Should().Be(RoomRole.Owner);
    }

    [Fact]
    public async Task ArchiveRoomAsync_ShouldReturnForbidden_WhenApprovedMembersRemain()
    {
        await using var ctx = await RoomServiceTestContext.CreateAsync();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var community = SeedCommunity(ctx, ownerId);
        var club = SeedClub(ctx, community.Id, ownerId);
        var room = SeedRoom(ctx, club.Id, ownerId, RoomJoinPolicy.Open, membersCount: 2);
        SeedMember(ctx, room.Id, ownerId, RoomRole.Owner, RoomMemberStatus.Approved);
        SeedMember(ctx, room.Id, memberId, RoomRole.Member, RoomMemberStatus.Approved);
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.ArchiveRoomAsync(ownerId, room.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Forbidden);
    }

    [Fact]
    public async Task KickRoomMemberAsync_ShouldCascadeRemoval_WhenOwnerRemovesApprovedMember()
    {
        await using var ctx = await RoomServiceTestContext.CreateAsync();
        var ownerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var community = SeedCommunity(ctx, ownerId);
        community.MembersCount = 2;
        var club = SeedClub(ctx, community.Id, ownerId);
        club.MembersCount = 2;
        var room = SeedRoom(ctx, club.Id, ownerId, RoomJoinPolicy.Open, membersCount: 2);

        SeedCommunityMember(ctx, community.Id, ownerId, MemberRole.Owner);
        SeedCommunityMember(ctx, community.Id, targetId, MemberRole.Member);
        SeedClubMember(ctx, club.Id, ownerId, MemberRole.Owner);
        SeedClubMember(ctx, club.Id, targetId, MemberRole.Member);
        SeedMember(ctx, room.Id, ownerId, RoomRole.Owner, RoomMemberStatus.Approved);
        SeedMember(ctx, room.Id, targetId, RoomRole.Member, RoomMemberStatus.Approved);
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.KickRoomMemberAsync(room.Id, targetId, ownerId);

        result.IsSuccess.Should().BeTrue();

        (await ctx.Db.RoomMembers.AnyAsync(m => m.RoomId == room.Id && m.UserId == targetId)).Should().BeFalse();
        (await ctx.Db.ClubMembers.AnyAsync(m => m.ClubId == club.Id && m.UserId == targetId)).Should().BeFalse();
        (await ctx.Db.CommunityMembers.AnyAsync(m => m.CommunityId == community.Id && m.UserId == targetId)).Should().BeFalse();

        (await ctx.Db.Rooms.FindAsync(room.Id))!.MembersCount.Should().Be(1);
        (await ctx.Db.Clubs.FindAsync(club.Id))!.MembersCount.Should().Be(1);
        (await ctx.Db.Communities.FindAsync(community.Id))!.MembersCount.Should().Be(1);
    }

    [Fact]
    public async Task KickRoomMemberAsync_ShouldReturnForbidden_WhenModeratorTargetsPeer()
    {
        await using var ctx = await RoomServiceTestContext.CreateAsync();
        var actorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var community = SeedCommunity(ctx, actorId);
        var club = SeedClub(ctx, community.Id, actorId);
        var room = SeedRoom(ctx, club.Id, actorId, RoomJoinPolicy.Open, membersCount: 2);

        SeedMember(ctx, room.Id, actorId, RoomRole.Moderator, RoomMemberStatus.Approved);
        SeedMember(ctx, room.Id, targetId, RoomRole.Moderator, RoomMemberStatus.Approved);
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.KickRoomMemberAsync(room.Id, targetId, actorId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Forbidden);
    }

    [Fact]
    public async Task KickRoomMemberAsync_ShouldReturnForbidden_WhenTargetIsRoomOwner()
    {
        await using var ctx = await RoomServiceTestContext.CreateAsync();
        var actorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var community = SeedCommunity(ctx, actorId);
        var club = SeedClub(ctx, community.Id, actorId);
        var room = SeedRoom(ctx, club.Id, actorId, RoomJoinPolicy.Open, membersCount: 2);

        SeedMember(ctx, room.Id, actorId, RoomRole.Owner, RoomMemberStatus.Approved);
        SeedMember(ctx, room.Id, targetId, RoomRole.Owner, RoomMemberStatus.Approved);
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.KickRoomMemberAsync(room.Id, targetId, actorId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Forbidden);
    }

    [Fact]
    public async Task KickRoomMemberAsync_ShouldReturnValidation_WhenActorKicksSelf()
    {
        await using var ctx = await RoomServiceTestContext.CreateAsync();
        var userId = Guid.NewGuid();

        var community = SeedCommunity(ctx, userId);
        var club = SeedClub(ctx, community.Id, userId);
        var room = SeedRoom(ctx, club.Id, userId, RoomJoinPolicy.Open, membersCount: 1);

        SeedMember(ctx, room.Id, userId, RoomRole.Owner, RoomMemberStatus.Approved);
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.KickRoomMemberAsync(room.Id, userId, userId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Validation);
    }

    [Fact]
    public async Task KickRoomMemberAsync_ShouldSucceed_WhenTargetAlreadyRemoved()
    {
        await using var ctx = await RoomServiceTestContext.CreateAsync();
        var ownerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var community = SeedCommunity(ctx, ownerId);
        community.MembersCount = 2;
        var club = SeedClub(ctx, community.Id, ownerId);
        club.MembersCount = 2;
        var room = SeedRoom(ctx, club.Id, ownerId, RoomJoinPolicy.Open, membersCount: 2);

        SeedCommunityMember(ctx, community.Id, ownerId, MemberRole.Owner);
        SeedCommunityMember(ctx, community.Id, targetId, MemberRole.Member);
        SeedClubMember(ctx, club.Id, ownerId, MemberRole.Owner);
        SeedClubMember(ctx, club.Id, targetId, MemberRole.Member);
        SeedMember(ctx, room.Id, ownerId, RoomRole.Owner, RoomMemberStatus.Approved);
        SeedMember(ctx, room.Id, targetId, RoomRole.Member, RoomMemberStatus.Approved);
        await ctx.Db.SaveChangesAsync();

        (await ctx.Service.KickRoomMemberAsync(room.Id, targetId, ownerId)).IsSuccess.Should().BeTrue();
        var second = await ctx.Service.KickRoomMemberAsync(room.Id, targetId, ownerId);

        second.IsSuccess.Should().BeTrue();
        (await ctx.Db.RoomMembers.AnyAsync(m => m.RoomId == room.Id && m.UserId == targetId)).Should().BeFalse();
        (await ctx.Db.ClubMembers.AnyAsync(m => m.ClubId == club.Id && m.UserId == targetId)).Should().BeFalse();
        (await ctx.Db.CommunityMembers.AnyAsync(m => m.CommunityId == community.Id && m.UserId == targetId)).Should().BeFalse();
    }

    [Fact]
    public async Task KickRoomMemberAsync_ShouldRollback_WhenTargetIsClubOwner()
    {
        await using var ctx = await RoomServiceTestContext.CreateAsync();
        var ownerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var community = SeedCommunity(ctx, ownerId);
        community.MembersCount = 2;
        var club = SeedClub(ctx, community.Id, ownerId);
        club.MembersCount = 2;
        var room = SeedRoom(ctx, club.Id, ownerId, RoomJoinPolicy.Open, membersCount: 2);

        SeedCommunityMember(ctx, community.Id, ownerId, MemberRole.Owner);
        SeedCommunityMember(ctx, community.Id, targetId, MemberRole.Member);
        SeedClubMember(ctx, club.Id, ownerId, MemberRole.Owner);
        SeedClubMember(ctx, club.Id, targetId, MemberRole.Owner);
        SeedMember(ctx, room.Id, ownerId, RoomRole.Owner, RoomMemberStatus.Approved);
        SeedMember(ctx, room.Id, targetId, RoomRole.Member, RoomMemberStatus.Approved);
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.KickRoomMemberAsync(room.Id, targetId, ownerId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Forbidden);

        (await ctx.Db.RoomMembers.AnyAsync(m => m.RoomId == room.Id && m.UserId == targetId)).Should().BeTrue();
        (await ctx.Db.ClubMembers.AnyAsync(m => m.ClubId == club.Id && m.UserId == targetId)).Should().BeTrue();
        (await ctx.Db.Clubs.FindAsync(club.Id))!.MembersCount.Should().Be(2);
    }

    [Fact]
    public async Task KickRoomMemberAsync_ShouldRollback_WhenTargetIsCommunityOwner()
    {
        await using var ctx = await RoomServiceTestContext.CreateAsync();
        var ownerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var community = SeedCommunity(ctx, ownerId);
        community.MembersCount = 2;
        var club = SeedClub(ctx, community.Id, ownerId);
        club.MembersCount = 2;
        var room = SeedRoom(ctx, club.Id, ownerId, RoomJoinPolicy.Open, membersCount: 2);

        SeedCommunityMember(ctx, community.Id, ownerId, MemberRole.Owner);
        SeedCommunityMember(ctx, community.Id, targetId, MemberRole.Owner);
        SeedClubMember(ctx, club.Id, ownerId, MemberRole.Owner);
        SeedClubMember(ctx, club.Id, targetId, MemberRole.Member);
        SeedMember(ctx, room.Id, ownerId, RoomRole.Owner, RoomMemberStatus.Approved);
        SeedMember(ctx, room.Id, targetId, RoomRole.Member, RoomMemberStatus.Approved);
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.KickRoomMemberAsync(room.Id, targetId, ownerId);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Forbidden);

        (await ctx.Db.RoomMembers.AnyAsync(m => m.RoomId == room.Id && m.UserId == targetId)).Should().BeTrue();
        (await ctx.Db.CommunityMembers.AnyAsync(m => m.CommunityId == community.Id && m.UserId == targetId)).Should().BeTrue();
        (await ctx.Db.Communities.FindAsync(community.Id))!.MembersCount.Should().Be(2);
    }

    private static Community SeedCommunity(RoomServiceTestContext ctx, Guid createdBy)
    {
        var community = new Community
        {
            Id = Guid.NewGuid(),
            Name = "Community",
            IsPublic = true,
            MembersCount = 0,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy
        };
        ctx.Db.Communities.Add(community);
        return community;
    }

    private static Club SeedClub(RoomServiceTestContext ctx, Guid communityId, Guid createdBy)
    {
        var club = new Club
        {
            Id = Guid.NewGuid(),
            CommunityId = communityId,
            Name = "Club",
            IsPublic = true,
            MembersCount = 0,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy
        };
        ctx.Db.Clubs.Add(club);
        return club;
    }

    private static Room SeedRoom(RoomServiceTestContext ctx, Guid clubId, Guid createdBy, RoomJoinPolicy policy, int membersCount)
    {
        var room = new Room
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            Name = "Room",
            JoinPolicy = policy,
            MembersCount = membersCount,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy
        };
        ctx.Db.Rooms.Add(room);
        return room;
    }

    private static void SeedMember(RoomServiceTestContext ctx, Guid roomId, Guid userId, RoomRole role, RoomMemberStatus status)
    {
        var member = new RoomMember
        {
            RoomId = roomId,
            UserId = userId,
            Role = role,
            Status = status,
            JoinedAt = DateTime.UtcNow
        };
        ctx.Db.RoomMembers.Add(member);
    }

    private static void SeedClubMember(RoomServiceTestContext ctx, Guid clubId, Guid userId, MemberRole role)
    {
        var member = new ClubMember
        {
            ClubId = clubId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        };
        ctx.Db.ClubMembers.Add(member);
    }

    private static void SeedCommunityMember(RoomServiceTestContext ctx, Guid communityId, Guid userId, MemberRole role)
    {
        var member = new CommunityMember
        {
            CommunityId = communityId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        };
        ctx.Db.CommunityMembers.Add(member);
    }

    private sealed class RoomServiceTestContext : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly IGenericUnitOfWork _uow;

        public required AppDbContext Db { get; init; }
        public required RoomService Service { get; init; }

        private RoomServiceTestContext(SqliteConnection connection, IGenericUnitOfWork uow)
        {
            _connection = connection;
            _uow = uow;
        }

        public static async Task<RoomServiceTestContext> CreateAsync()
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
            var queryRepo = new RoomQueryRepository(db);
            var commandRepo = new RoomCommandRepository(db);
            var clubQuery = new ClubQueryRepository(db);
            var clubCommand = new ClubCommandRepository(db);
            var communityQuery = new CommunityQueryRepository(db);
            var communityCommand = new CommunityCommandRepository(db);
            var passwordHasher = new PasswordHasher<Room>();
            var service = new RoomService(
                uow,
                queryRepo,
                commandRepo,
                clubQuery,
                clubCommand,
                communityQuery,
                communityCommand,
                passwordHasher,
                NullLogger<RoomService>.Instance);

            return new RoomServiceTestContext(connection, uow)
            {
                Db = db,
                Service = service
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
