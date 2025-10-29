using BusinessObjects;
using BusinessObjects.Common;

namespace WebApi.Payments.Tests.Infrastructure;

internal static class TestEntityFactory
{
    public static User CreateUser(Guid id) => new()
    {
        Id = id,
        UserName = "test-user",
        NormalizedUserName = "TEST-USER",
        Email = "test-user@studentgamerhub.local",
        NormalizedEmail = "TEST-USER@STUDENTGAMERHUB.LOCAL",
        SecurityStamp = Guid.NewGuid().ToString(),
        CreatedAtUtc = DateTime.UtcNow,
        CreatedBy = id
    };

    public static Wallet CreateWallet(Guid userId, long balanceCents) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        BalanceCents = balanceCents,
        CreatedAtUtc = DateTime.UtcNow,
        CreatedBy = userId
    };

    public static Event CreateEvent(Guid organizerId, long priceCents = 0) => new()
    {
        Id = Guid.NewGuid(),
        OrganizerId = organizerId,
        Title = "Test Event",
        Description = "Test event description",
        Mode = EventMode.Online,
        StartsAt = DateTime.UtcNow.AddDays(1),
        EndsAt = DateTime.UtcNow.AddDays(1).AddHours(2),
        PriceCents = priceCents,
        Capacity = 100,
        Status = EventStatus.Open,
        CreatedAtUtc = DateTime.UtcNow,
        CreatedBy = organizerId
    };
}
