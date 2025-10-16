using BusinessObjects;
using BusinessObjects.Common;
using BusinessObjects.Common.Results;
using DTOs.Communities;
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

public sealed class CommunityServiceTests
{
    [Fact]
    public async Task CreateAsync_ShouldCreateCommunityWithDefaults()
    {
        await using var ctx = await CommunityServiceTestContext.CreateAsync();
        var userId = Guid.NewGuid();
        var request = new CommunityCreateRequestDto(null, "My Community", "Fun place", "  Uni  ", true);

        var result = await ctx.Service.CreateAsync(userId, request);

        result.IsSuccess.Should().BeTrue();
        var community = await ctx.Db.Communities.SingleAsync(c => c.Id == result.Value);
        community.Name.Should().Be("My Community");
        community.Description.Should().Be("Fun place");
        community.School.Should().Be("Uni");
        community.IsPublic.Should().BeTrue();
        community.MembersCount.Should().Be(0);
        community.CreatedBy.Should().Be(userId);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateFields()
    {
        await using var ctx = await CommunityServiceTestContext.CreateAsync();
        var creatorId = Guid.NewGuid();
        var community = new Community
        {
            Id = Guid.NewGuid(),
            Name = "Old Name",
            Description = "Old",
            School = "Old School",
            IsPublic = true,
            MembersCount = 5,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = creatorId
        };
        ctx.Db.Communities.Add(community);
        await ctx.Db.SaveChangesAsync();

        var updaterId = Guid.NewGuid();
        var request = new CommunityUpdateRequestDto("  New Name  ", "  New Description  ", "", false);

        var result = await ctx.Service.UpdateAsync(updaterId, community.Id, request);

        result.IsSuccess.Should().BeTrue();
        var updated = await ctx.Db.Communities.SingleAsync(c => c.Id == community.Id);
        updated.Name.Should().Be("New Name");
        updated.Description.Should().Be("New Description");
        updated.School.Should().BeNull();
        updated.IsPublic.Should().BeFalse();
        updated.MembersCount.Should().BeGreaterOrEqualTo(0);
        updated.UpdatedBy.Should().Be(updaterId);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnDetailDto()
    {
        await using var ctx = await CommunityServiceTestContext.CreateAsync();
        var community = new Community
        {
            Id = Guid.NewGuid(),
            Name = "Detail",
            Description = "Desc",
            School = "School",
            IsPublic = false,
            MembersCount = 7,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        ctx.Db.Communities.Add(community);
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.GetByIdAsync(community.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(community.Id);
        result.Value.Name.Should().Be("Detail");
        result.Value.Description.Should().Be("Desc");
        result.Value.School.Should().Be("School");
        result.Value.IsPublic.Should().BeFalse();
        result.Value.MembersCount.Should().Be(7);
    }

    [Fact]
    public async Task ArchiveAsync_ShouldReturnForbidden_WhenApprovedMembersRemain()
    {
        await using var ctx = await CommunityServiceTestContext.CreateAsync();
        var community = new Community
        {
            Id = Guid.NewGuid(),
            Name = "Archive",
            IsPublic = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        var club = new Club
        {
            Id = Guid.NewGuid(),
            CommunityId = community.Id,
            Name = "Club",
            IsPublic = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        var room = new Room
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            Name = "Room",
            JoinPolicy = RoomJoinPolicy.Open,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        var roomMember = new RoomMember
        {
            RoomId = room.Id,
            UserId = Guid.NewGuid(),
            Status = RoomMemberStatus.Approved,
            Role = RoomRole.Member,
            JoinedAt = DateTime.UtcNow
        };
        ctx.Db.Communities.Add(community);
        ctx.Db.Clubs.Add(club);
        ctx.Db.Rooms.Add(room);
        ctx.Db.RoomMembers.Add(roomMember);
        await ctx.Db.SaveChangesAsync();

        var result = await ctx.Service.ArchiveAsync(Guid.NewGuid(), community.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be(Error.Codes.Forbidden);
    }

    [Fact]
    public async Task ArchiveAsync_ShouldSoftDeleteCommunity_WhenNoApprovedMembers()
    {
        await using var ctx = await CommunityServiceTestContext.CreateAsync();
        var community = new Community
        {
            Id = Guid.NewGuid(),
            Name = "Archive",
            IsPublic = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        ctx.Db.Communities.Add(community);
        await ctx.Db.SaveChangesAsync();

        var userId = Guid.NewGuid();
        var result = await ctx.Service.ArchiveAsync(userId, community.Id);

        result.IsSuccess.Should().BeTrue();
        var archived = await ctx.Db.Communities.IgnoreQueryFilters().SingleAsync(c => c.Id == community.Id);
        archived.IsDeleted.Should().BeTrue();
        archived.DeletedBy.Should().Be(userId);
        archived.DeletedAtUtc.Should().NotBeNull();
    }

    private sealed class CommunityServiceTestContext : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly IGenericUnitOfWork _uow;

        public required AppDbContext Db { get; init; }
        public required CommunityService Service { get; init; }

        private CommunityServiceTestContext(SqliteConnection connection, IGenericUnitOfWork uow)
        {
            _connection = connection;
            _uow = uow;
        }

        public static async Task<CommunityServiceTestContext> CreateAsync()
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
            var queryRepo = new CommunityQueryRepository(db);
            var commandRepo = new CommunityCommandRepository(db);
            var service = new CommunityService(uow, queryRepo, commandRepo);

            return new CommunityServiceTestContext(connection, uow)
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
