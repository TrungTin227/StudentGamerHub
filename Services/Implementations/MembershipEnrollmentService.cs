namespace Services.Implementations;

public sealed class MembershipEnrollmentService : IMembershipEnrollmentService
{
    private readonly IUserMembershipRepository _userMembershipRepository;

    public MembershipEnrollmentService(IUserMembershipRepository userMembershipRepository)
    {
        _userMembershipRepository = userMembershipRepository ?? throw new ArgumentNullException(nameof(userMembershipRepository));
    }

    public async Task<UserMembershipInfoDto> AssignAsync(Guid userId, MembershipPlan plan, Guid actorId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId must be provided.", nameof(userId));
        }

        var now = DateTime.UtcNow;
        var startDate = now;
        var durationMonths = Math.Max(1, plan.DurationMonths);
        var endDate = startDate.AddMonths(durationMonths);
        var quota = plan.MonthlyEventLimit == -1 ? int.MaxValue : plan.MonthlyEventLimit;

        var membership = await _userMembershipRepository.GetForUpdateAsync(userId, ct).ConfigureAwait(false);

        if (membership is null)
        {
            membership = new UserMembership
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MembershipPlanId = plan.Id,
                StartDate = startDate,
                EndDate = endDate,
                RemainingEventQuota = quota,
                LastResetAtUtc = now,
                CreatedAtUtc = now,
                CreatedBy = actorId,
            };

            await _userMembershipRepository.AddAsync(membership, ct).ConfigureAwait(false);
        }
        else
        {
            membership.MembershipPlanId = plan.Id;
            membership.StartDate = startDate;
            membership.EndDate = endDate;
            membership.RemainingEventQuota = quota;
            membership.LastResetAtUtc = now;
            membership.UpdatedAtUtc = now;
            membership.UpdatedBy = actorId;

            await _userMembershipRepository.UpdateAsync(membership).ConfigureAwait(false);
        }

        membership.MembershipPlan = plan;
        return membership.ToInfoDto(now);
    }
}
