using BusinessObjects;
using FluentAssertions;
using Services.Common.Mapping;
using Xunit;

namespace Services.Memberships.Tests;

public sealed class MembershipPlanMapperTests
{
    [Fact]
    public void ToInfoDto_ShouldExposeNullQuota_ForUnlimitedPlan()
    {
        var now = DateTime.UtcNow;
        var plan = new MembershipPlan
        {
            Id = Guid.NewGuid(),
            Name = "Unlimited",
            MonthlyEventLimit = -1,
            Price = 0,
            DurationMonths = 1,
            CreatedAtUtc = now
        };

        var membership = new UserMembership
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            MembershipPlanId = plan.Id,
            MembershipPlan = plan,
            StartDate = now.AddDays(-1),
            EndDate = now.AddMonths(1),
            RemainingEventQuota = int.MaxValue,
            LastResetAtUtc = now.AddDays(-1),
            CreatedAtUtc = now,
        };

        var dto = membership.ToInfoDto(now);

        dto.MonthlyEventLimit.Should().Be(-1);
        dto.RemainingEventQuota.Should().BeNull();
    }

    [Fact]
    public void ToInfoDto_ShouldExposeNumericQuota_ForLimitedPlan()
    {
        var now = DateTime.UtcNow;
        var plan = new MembershipPlan
        {
            Id = Guid.NewGuid(),
            Name = "Starter",
            MonthlyEventLimit = 5,
            Price = 0,
            DurationMonths = 1,
            CreatedAtUtc = now
        };

        var membership = new UserMembership
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            MembershipPlanId = plan.Id,
            MembershipPlan = plan,
            StartDate = now.AddDays(-1),
            EndDate = now.AddMonths(1),
            RemainingEventQuota = 3,
            LastResetAtUtc = now.AddDays(-1),
            CreatedAtUtc = now,
        };

        var dto = membership.ToInfoDto(now);

        dto.MonthlyEventLimit.Should().Be(5);
        dto.RemainingEventQuota.Should().Be(3);
    }
}
