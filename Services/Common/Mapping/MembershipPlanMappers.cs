using DTOs.Memberships;

namespace Services.Common.Mapping;

public static class MembershipPlanMappers
{
    public static MembershipPlanSummaryDto ToSummaryDto(this MembershipPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new MembershipPlanSummaryDto(
            plan.Id,
            plan.Name,
            plan.Description,
            plan.MonthlyEventLimit,
            plan.Price,
            plan.DurationMonths,
            plan.IsActive);
    }

    public static MembershipPlanDetailDto ToDetailDto(this MembershipPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new MembershipPlanDetailDto(
            plan.Id,
            plan.Name,
            plan.Description,
            plan.MonthlyEventLimit,
            plan.Price,
            plan.DurationMonths,
            plan.IsActive,
            plan.CreatedAtUtc,
            plan.UpdatedAtUtc);
    }

    public static UserMembershipInfoDto ToInfoDto(this UserMembership membership, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(membership);
        var plan = membership.MembershipPlan ?? throw new InvalidOperationException("Membership plan navigation must be loaded to project membership info.");
        var isActive = membership.EndDate >= utcNow;
        var remainingQuota = plan.MonthlyEventLimit == -1 ? (int?)null : membership.RemainingEventQuota;

        return new UserMembershipInfoDto(
            membership.MembershipPlanId,
            plan.Name,
            plan.MonthlyEventLimit,
            membership.StartDate,
            membership.EndDate,
            remainingQuota,
            isActive);
    }
}
