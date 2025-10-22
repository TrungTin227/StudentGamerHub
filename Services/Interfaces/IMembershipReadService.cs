using Services.DTOs.Memberships;

namespace Services.Interfaces;

public interface IMembershipReadService
{
    Task<Result<MembershipTreeHybridDto>> GetMyMembershipTreeAsync(Guid userId, CancellationToken ct = default);
}
