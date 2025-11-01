namespace Services.Interfaces;

public interface IMembershipEnrollmentService
{
    Task<UserMembershipInfoDto> AssignAsync(Guid userId, MembershipPlan plan, Guid actorId, CancellationToken ct = default);
}
