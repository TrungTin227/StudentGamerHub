namespace Services.Interfaces;

public interface IMembershipPlanService
{
    Task<Result<PagedResult<MembershipPlanSummaryDto>>> GetAllAsync(PageRequest pageRequest, bool includeInactive, CancellationToken ct = default);
    Task<Result<IReadOnlyList<MembershipPlanSummaryDto>>> GetPublicAsync(CancellationToken ct = default);
    Task<Result<MembershipPlanDetailDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<MembershipPlanDetailDto>> CreateAsync(MembershipPlanCreateRequest request, Guid actorId, CancellationToken ct = default);
    Task<Result<MembershipPlanDetailDto>> UpdateAsync(Guid id, MembershipPlanUpdateRequest request, Guid actorId, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid actorId, CancellationToken ct = default);
}
