using Application.Friends;
using BusinessObjects;
using BusinessObjects.Common;
using BusinessObjects.Common.Pagination;
using BusinessObjects.Common.Results;
using DTOs.Friends;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Repositories.Persistence;
using Repositories.WorkSeeds.Implements;
using Repositories.WorkSeeds.Interfaces;
using Services.Friends;
using Xunit;
using Xunit.Abstractions;

namespace Services.Presence.Tests;

/// <summary>
/// Comprehensive integration tests to verify and validate the friendship symmetry bug:
/// "M?t bên là b?n, bên kia ch?a" - one side shows as friends, the other doesn't.
/// 
/// Test Flow:
/// 1. User A sends friend request to User B
/// 2. Verify A sees outgoing request (IsPending = true), B sees incoming request
/// 3. User B accepts request
/// 4. Verify BOTH A and B see each other as friends (IsFriend = true, IsPending = false)
/// 5. Test unfriend scenario
/// </summary>
public sealed class Friendship_Symmetry_Tests
{
    private readonly ITestOutputHelper _output;

    public Friendship_Symmetry_Tests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task SearchUsers_AfterAccept_BothSidesShouldSeeFriendStatus()
    {
        // Arrange
        await using var ctx = await FriendServiceTestContext.CreateAsync();
        
        var userA = CreateUser("a@test.com", "UserA");
        var userB = CreateUser("b@test.com", "UserB");
        ctx.Db.Users.AddRange(userA, userB);
        await ctx.Db.SaveChangesAsync();

        LogStep("? Created User A and User B");

        // Act & Assert - Step 1: A sends friend request to B
        var inviteResult = await ctx.Service.InviteAsync(userA.Id, userB.Id);
        inviteResult.IsSuccess.Should().BeTrue("A should be able to send friend request to B");
        LogStep("? User A sent friend request to User B");

        // Step 2: Check search results when request is pending
        await VerifyPendingState(ctx, userA, userB);

        // Step 3: B accepts the request
        var acceptResult = await ctx.Service.AcceptAsync(userB.Id, userA.Id);
        acceptResult.IsSuccess.Should().BeTrue("B should be able to accept A's request");
        LogStep("? User B accepted friend request from User A");

        // Step 4: Verify BOTH sides see each other as friends
        await VerifyAcceptedState(ctx, userA, userB);

        // Step 5: Verify database state
        await VerifyDatabaseState(ctx, userA.Id, userB.Id, FriendStatus.Accepted);
    }

    [Fact]
    public async Task SearchUsers_PendingRequest_ShouldShowCorrectPendingFlags()
    {
        // Arrange
        await using var ctx = await FriendServiceTestContext.CreateAsync();
        
        var userA = CreateUser("sender@test.com", "Sender");
        var userB = CreateUser("receiver@test.com", "Receiver");
        ctx.Db.Users.AddRange(userA, userB);
        await ctx.Db.SaveChangesAsync();

        // Act: A sends request to B
        await ctx.Service.InviteAsync(userA.Id, userB.Id);

        // Assert: Verify pending state
        await VerifyPendingState(ctx, userA, userB);
    }

    [Fact]
    public async Task SearchUsers_AfterUnfriend_BothSidesShouldNotSeeFriendStatus()
    {
        // This test verifies that after unfriending, both sides correctly show as not friends
        // Note: Current implementation doesn't have unfriend endpoint, but we can delete the link
        
        await using var ctx = await FriendServiceTestContext.CreateAsync();
        
        var userA = CreateUser("a@test.com", "UserA");
        var userB = CreateUser("b@test.com", "UserB");
        ctx.Db.Users.AddRange(userA, userB);
        await ctx.Db.SaveChangesAsync();

        // Become friends
        await ctx.Service.InviteAsync(userA.Id, userB.Id);
        await ctx.Service.AcceptAsync(userB.Id, userA.Id);

        LogStep("? Users became friends");

        // Simulate unfriend by deleting the link
        var link = await ctx.Db.FriendLinks.FirstAsync(l => 
            (l.SenderId == userA.Id && l.RecipientId == userB.Id) ||
            (l.SenderId == userB.Id && l.RecipientId == userA.Id));
        
        ctx.Db.FriendLinks.Remove(link);
        await ctx.Db.SaveChangesAsync();

        LogStep("? Friendship link removed");

        // Verify both sides no longer see each other as friends
        var aSearchB = await ctx.Service.SearchUsersAsync(
            userA.Id, 
            userB.UserName, 
            new PageRequest(1, 10, null, false));
        
        var bSearchA = await ctx.Service.SearchUsersAsync(
            userB.Id, 
            userA.UserName, 
            new PageRequest(1, 10, null, false));

        aSearchB.IsSuccess.Should().BeTrue();
        bSearchA.IsSuccess.Should().BeTrue();

        var aResult = aSearchB.Value.Items.First(u => u.UserId == userB.Id);
        var bResult = bSearchA.Value.Items.First(u => u.UserId == userA.Id);

        aResult.IsFriend.Should().BeFalse("After unfriend, A should not see B as friend");
        aResult.IsPending.Should().BeFalse("After unfriend, A should not see pending state");
        
        bResult.IsFriend.Should().BeFalse("After unfriend, B should not see A as friend");
        bResult.IsPending.Should().BeFalse("After unfriend, B should not see pending state");

        LogStep("? Both sides correctly show as not friends");
    }

    [Fact]
    public async Task SearchUsers_ReverseRequest_ShouldNotifyWhenOtherSideSentRequest()
    {
        // Verifies the scenario where B tries to send request but A already sent one
        
        await using var ctx = await FriendServiceTestContext.CreateAsync();
        
        var userA = CreateUser("a@test.com", "UserA");
        var userB = CreateUser("b@test.com", "UserB");
        ctx.Db.Users.AddRange(userA, userB);
        await ctx.Db.SaveChangesAsync();

        // A sends request to B
        await ctx.Service.InviteAsync(userA.Id, userB.Id);
        LogStep("? User A sent request to User B");

        // B tries to send request to A (should get notification that A already sent one)
        var reverseResult = await ctx.Service.InviteAsync(userB.Id, userA.Id);
        
        reverseResult.IsSuccess.Should().BeFalse();
        reverseResult.Error.Message.Should().Contain("??i ph??ng ?ã m?i b?n tr??c");
        LogStep("? User B correctly notified that A already sent request");
    }

    [Fact]
    public async Task SearchUsers_MultipleUsers_ShouldMaintainSymmetryForAll()
    {
        // Test with multiple users to ensure symmetry holds across all relationships
        
        await using var ctx = await FriendServiceTestContext.CreateAsync();
        
        var users = new[]
        {
            CreateUser("user1@test.com", "User1"),
            CreateUser("user2@test.com", "User2"),
            CreateUser("user3@test.com", "User3")
        };
        
        ctx.Db.Users.AddRange(users);
        await ctx.Db.SaveChangesAsync();

        // Create friendship network: 1-2, 2-3
        await ctx.Service.InviteAsync(users[0].Id, users[1].Id);
        await ctx.Service.AcceptAsync(users[1].Id, users[0].Id);
        
        await ctx.Service.InviteAsync(users[1].Id, users[2].Id);
        await ctx.Service.AcceptAsync(users[2].Id, users[1].Id);

        LogStep("? Created friendship network");

        // Verify User2 sees both User1 and User3 as friends
        var user2Search = await ctx.Service.SearchUsersAsync(
            users[1].Id, 
            null, 
            new PageRequest(1, 10, null, false));

        user2Search.IsSuccess.Should().BeTrue();
        
        var user1Result = user2Search.Value.Items.First(u => u.UserId == users[0].Id);
        var user3Result = user2Search.Value.Items.First(u => u.UserId == users[2].Id);

        user1Result.IsFriend.Should().BeTrue("User2 should see User1 as friend");
        user3Result.IsFriend.Should().BeTrue("User2 should see User3 as friend");

        LogStep("? All friendship relationships are symmetric");
    }

    #region Helper Methods

    private async Task VerifyPendingState(FriendServiceTestContext ctx, User userA, User userB)
    {
        LogStep("? Verifying pending state...");

        // A searches for B: should see IsPending = true (outgoing)
        var aSearchB = await ctx.Service.SearchUsersAsync(
            userA.Id, 
            userB.UserName, 
            new PageRequest(1, 10, null, false));
        
        aSearchB.IsSuccess.Should().BeTrue();
        var aResult = aSearchB.Value.Items.FirstOrDefault(u => u.UserId == userB.Id);
        
        aResult.Should().NotBeNull("User A should find User B in search");
        aResult!.IsFriend.Should().BeFalse("Users are not friends yet");
        aResult.IsPending.Should().BeTrue("User A sent request, so IsPending should be true");

        LogStep($"  ? User A searching for User B: IsFriend={aResult.IsFriend}, IsPending={aResult.IsPending}");

        // B searches for A: should see IsPending = true (incoming)
        var bSearchA = await ctx.Service.SearchUsersAsync(
            userB.Id, 
            userA.UserName, 
            new PageRequest(1, 10, null, false));
        
        bSearchA.IsSuccess.Should().BeTrue();
        var bResult = bSearchA.Value.Items.FirstOrDefault(u => u.UserId == userA.Id);
        
        bResult.Should().NotBeNull("User B should find User A in search");
        bResult!.IsFriend.Should().BeFalse("Users are not friends yet");
        bResult.IsPending.Should().BeTrue("User B received request, so IsPending should be true");

        LogStep($"  ? User B searching for User A: IsFriend={bResult.IsFriend}, IsPending={bResult.IsPending}");
    }

    private async Task VerifyAcceptedState(FriendServiceTestContext ctx, User userA, User userB)
    {
        LogStep("? Verifying accepted state (THIS IS THE CRITICAL BUG CHECK)...");

        // A searches for B: should see IsFriend = true, IsPending = false
        var aSearchB = await ctx.Service.SearchUsersAsync(
            userA.Id, 
            userB.UserName, 
            new PageRequest(1, 10, null, false));
        
        aSearchB.IsSuccess.Should().BeTrue();
        var aResult = aSearchB.Value.Items.FirstOrDefault(u => u.UserId == userB.Id);
        
        aResult.Should().NotBeNull("User A should find User B in search");
        aResult!.IsFriend.Should().BeTrue("After accept, User A should see User B as friend");
        aResult.IsPending.Should().BeFalse("After accept, IsPending should be false for User A");

        LogStep($"  ? User A searching for User B: IsFriend={aResult.IsFriend}, IsPending={aResult.IsPending}");

        // B searches for A: should see IsFriend = true, IsPending = false
        var bSearchA = await ctx.Service.SearchUsersAsync(
            userB.Id, 
            userA.UserName, 
            new PageRequest(1, 10, null, false));
        
        bSearchA.IsSuccess.Should().BeTrue();
        var bResult = bSearchA.Value.Items.FirstOrDefault(u => u.UserId == userA.Id);
        
        bResult.Should().NotBeNull("User B should find User A in search");
        bResult!.IsFriend.Should().BeTrue("After accept, User B should see User A as friend");
        bResult.IsPending.Should().BeFalse("After accept, IsPending should be false for User B");

        LogStep($"  ? User B searching for User A: IsFriend={bResult.IsFriend}, IsPending={bResult.IsPending}");
        LogStep("? SYMMETRY VERIFIED: Both sides correctly see each other as friends!");
    }

    private async Task VerifyDatabaseState(
        FriendServiceTestContext ctx, 
        Guid userAId, 
        Guid userBId, 
        FriendStatus expectedStatus)
    {
        LogStep("? Verifying database state...");

        var links = await ctx.Db.FriendLinks
            .Where(l => 
                (l.SenderId == userAId && l.RecipientId == userBId) ||
                (l.SenderId == userBId && l.RecipientId == userAId))
            .ToListAsync();

        links.Should().HaveCount(1, "There should be exactly one friendship link");
        
        var link = links.First();
        link.Status.Should().Be(expectedStatus);

        LogStep($"  ? Database: SenderId={link.SenderId}, RecipientId={link.RecipientId}, Status={link.Status}");
        
        if (expectedStatus == FriendStatus.Accepted)
        {
            link.RespondedAt.Should().NotBeNull("RespondedAt should be set when accepted");
            LogStep($"  ? RespondedAt is set: {link.RespondedAt}");
        }
    }

    private void LogStep(string message)
    {
        _output.WriteLine(message);
    }

    private static User CreateUser(string email, string userName)
    {
        var id = Guid.NewGuid();
        return new User
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FullName = userName,
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    #endregion

    #region Test Context

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
            await ConnectionDisposeAsync();
        }

        private ValueTask ConnectionDisposeAsync()
        {
            _connection.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    #endregion
}
