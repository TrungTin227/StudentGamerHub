using System.Threading.RateLimiting;
using FluentAssertions;
using Xunit;

namespace Services.Communities.Tests;

public sealed class RateLimiterTests
{
    [Fact]
    public void CommunitiesWritePolicy_ShouldRejectAfterTenRequests()
    {
        using var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = 10,
            TokensPerPeriod = 10,
            ReplenishmentPeriod = TimeSpan.FromDays(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
            AutoReplenishment = true
        });

        for (var i = 0; i < 10; i++)
        {
            using var lease = limiter.AttemptAcquire(1);
            lease.IsAcquired.Should().BeTrue();
        }

        using var finalLease = limiter.AttemptAcquire(1);
        finalLease.IsAcquired.Should().BeFalse();
    }
}
