namespace Repositories.Interfaces;

public interface IMembershipPlanRepository
{
    Task<IReadOnlyList<MembershipPlan>> GetAllAsync(bool includeInactive, CancellationToken ct = default);
    IQueryable<MembershipPlan> Query(bool includeInactive);
    Task<MembershipPlan?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<MembershipPlan?> GetForUpdateAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken ct = default);
    Task AddAsync(MembershipPlan plan, CancellationToken ct = default);
    Task UpdateAsync(MembershipPlan plan);
}
