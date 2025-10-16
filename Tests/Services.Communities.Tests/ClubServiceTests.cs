using BusinessObjects;
using BusinessObjects.Common;
using BusinessObjects.Common.Results;
using DTOs.Clubs;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Implements;
using Repositories.Persistence;
using Repositories.WorkSeeds.Implements;
using Repositories.WorkSeeds.Interfaces;
using Services.Implementations;
using Xunit;

namespace Services.Communities.Tests;

public sealed class ClubServiceTests
{
    [Fact]
    public async Task GetByIdAsync_ShouldReturnDetailDto()
    {
        await using var ctx = await ClubServiceTestContext.CreateAsync();
        var community = new Community
        {
            Id = Guid.NewGuid(),
            Name = "Test Community",
            IsPublic = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        var club = new Club
        {
            Id = Guid.NewGuid(),
            CommunityId = community.Id,
            Name = "Detail Club",
            Description = "Description",
            IsPublic = false,
            MembersCount = 5,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        ctx.Db.Communities.Add(community);
        ctx.Db.Clubs.Add(club);
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.GetByIdAsync(club.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(club.Id);
        result.Value.Name.Should().Be("Detail Club");
        result.Value.Description.Should().Be("Description");
        result.Value.MembersCount.Should().Be(5);
        result.Value.IsPublic.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnValidationError_WhenNameIsEmpty()
    {
        await using var ctx = await ClubServiceTestContext.CreateAsync();
        var userId = Guid.NewGuid();
        var community = new Community
        {
            Id = Guid.NewGuid(),
            Name = "Community",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = userId
        };
        var club = new Club
        {
            Id = Guid.NewGuid(),
            CommunityId = community.Id,
            Name = "Club",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = userId
        };
        ctx.Db.Communities.Add(community);
        ctx.Db.Clubs.Add(club);
        await ctx.Db.SaveChangesAsync();

        var request = new ClubUpdateRequestDto("  ", "New description", true);

        var result = await ctx.Service.UpdateAsync(userId, club.Id, request);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Validation);
    }

    [Fact]
    public async Task ArchiveAsync_ShouldReturnForbidden_WhenApprovedMembersRemain()
    {
        await using var ctx = await ClubServiceTestContext.CreateAsync();
        var userId = Guid.NewGuid();
        var community = new Community
        {
            Id = Guid.NewGuid(),
            Name = "Community",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = userId
        };
        var club = new Club
        {
            Id = Guid.NewGuid(),
            CommunityId = community.Id,
            Name = "Club",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = userId
        };
        var room = new Room
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            Name = "Room",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = userId
        };
        var member = new RoomMember
        {
            RoomId = room.Id,
            UserId = Guid.NewGuid(),
            Status = RoomMemberStatus.Approved,
            Role = RoomRole.Member,
            JoinedAt = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = userId
        };
        ctx.Db.Communities.Add(community);
        ctx.Db.Clubs.Add(club);
        ctx.Db.Rooms.Add(room);
        ctx.Db.RoomMembers.Add(member);
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.ArchiveAsync(userId, club.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Forbidden);
    }

    private sealed class ClubServiceTestContext : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly IGenericUnitOfWork _uow;

        public required AppDbContext Db { get; init; }
        public required ClubService Service { get; init; }

        private ClubServiceTestContext(SqliteConnection connection, IGenericUnitOfWork uow)
        {
            _connection = connection;
            _uow = uow;
        }

        public static async Task<ClubServiceTestContext> CreateAsync()
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
            var queryRepo = new ClubQueryRepository(db);
            var commandRepo = new ClubCommandRepository(db);
            var service = new ClubService(uow, queryRepo, commandRepo);

            return new ClubServiceTestContext(connection, uow)
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
