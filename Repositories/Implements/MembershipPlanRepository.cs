using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Repositories.Implements;

public sealed class MembershipPlanRepository : IMembershipPlanRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<MembershipPlanRepository> _logger;

    public MembershipPlanRepository(AppDbContext context, ILogger<MembershipPlanRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<MembershipPlan>> GetAllAsync(bool includeInactive, CancellationToken ct = default)
    {
        IQueryable<MembershipPlan> query = _context.MembershipPlans.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        return await query
            .OrderByDescending(p => p.IsActive)
            .ThenBy(p => p.Price)
            .ThenBy(p => p.Name)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public Task<MembershipPlan?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.MembershipPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<MembershipPlan?> GetForUpdateAsync(Guid id, CancellationToken ct = default)
        => _context.MembershipPlans
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        IQueryable<MembershipPlan> query = _context.MembershipPlans.AsNoTracking()
            .Where(p => p.Name == name);

        if (excludeId.HasValue)
        {
            query = query.Where(p => p.Id != excludeId.Value);
        }

        return await query.AnyAsync(ct).ConfigureAwait(false);
    }

    public async Task AddAsync(MembershipPlan plan, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        await _context.MembershipPlans.AddAsync(plan, ct).ConfigureAwait(false);
        _logger.LogInformation("Prepared new membership plan {PlanId} - {PlanName} for creation.", plan.Id, plan.Name);
    }

    public Task UpdateAsync(MembershipPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        _context.MembershipPlans.Update(plan);
        _logger.LogInformation("Prepared membership plan {PlanId} for update.", plan.Id);
        return Task.CompletedTask;
    }
}
