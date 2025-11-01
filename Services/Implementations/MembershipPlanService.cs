using Microsoft.Extensions.Logging;

namespace Services.Implementations;

public sealed class MembershipPlanService : IMembershipPlanService
{
    private readonly IGenericUnitOfWork _uow;
    private readonly IMembershipPlanRepository _membershipPlans;
    private readonly ILogger<MembershipPlanService> _logger;

    public MembershipPlanService(
        IGenericUnitOfWork uow,
        IMembershipPlanRepository membershipPlans,
        ILogger<MembershipPlanService> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _membershipPlans = membershipPlans ?? throw new ArgumentNullException(nameof(membershipPlans));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<IReadOnlyList<MembershipPlanSummaryDto>>> GetAllAsync(bool includeInactive, CancellationToken ct = default)
    {
        var plans = await _membershipPlans.GetAllAsync(includeInactive, ct).ConfigureAwait(false);
        var dtos = plans.Select(p => p.ToSummaryDto()).ToList();
        return Result<IReadOnlyList<MembershipPlanSummaryDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<MembershipPlanSummaryDto>>> GetPublicAsync(CancellationToken ct = default)
    {
        var plans = await _membershipPlans.GetAllAsync(includeInactive: false, ct).ConfigureAwait(false);
        var dtos = plans.Where(p => p.IsActive).Select(p => p.ToSummaryDto()).ToList();
        return Result<IReadOnlyList<MembershipPlanSummaryDto>>.Success(dtos);
    }

    public async Task<Result<MembershipPlanDetailDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await _membershipPlans.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (plan is null)
        {
            return Result<MembershipPlanDetailDto>.Failure(new Error(Error.Codes.NotFound, "Membership plan not found."));
        }

        return Result<MembershipPlanDetailDto>.Success(plan.ToDetailDto());
    }

    public async Task<Result<MembershipPlanDetailDto>> CreateAsync(MembershipPlanCreateRequest request, Guid actorId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return Result<MembershipPlanDetailDto>.Failure(new Error(Error.Codes.Validation, "Name is required."));
        }

        var exists = await _membershipPlans.ExistsByNameAsync(normalizedName, null, ct).ConfigureAwait(false);
        if (exists)
        {
            return Result<MembershipPlanDetailDto>.Failure(new Error(Error.Codes.Conflict, "A membership plan with this name already exists."));
        }

        var now = DateTime.UtcNow;

        var plan = new MembershipPlan
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            MonthlyEventLimit = request.MonthlyEventLimit,
            Price = request.Price,
            DurationMonths = request.DurationMonths,
            IsActive = request.IsActive,
            CreatedAtUtc = now,
            CreatedBy = actorId,
        };

        await _membershipPlans.AddAsync(plan, ct).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Created membership plan {PlanId} ({PlanName}).", plan.Id, plan.Name);

        return Result<MembershipPlanDetailDto>.Success(plan.ToDetailDto());
    }

    public async Task<Result<MembershipPlanDetailDto>> UpdateAsync(Guid id, MembershipPlanUpdateRequest request, Guid actorId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var plan = await _membershipPlans.GetForUpdateAsync(id, ct).ConfigureAwait(false);
        if (plan is null)
        {
            return Result<MembershipPlanDetailDto>.Failure(new Error(Error.Codes.NotFound, "Membership plan not found."));
        }

        var normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return Result<MembershipPlanDetailDto>.Failure(new Error(Error.Codes.Validation, "Name is required."));
        }

        var exists = await _membershipPlans.ExistsByNameAsync(normalizedName, id, ct).ConfigureAwait(false);
        if (exists)
        {
            return Result<MembershipPlanDetailDto>.Failure(new Error(Error.Codes.Conflict, "A membership plan with this name already exists."));
        }

        plan.Name = normalizedName;
        plan.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        plan.MonthlyEventLimit = request.MonthlyEventLimit;
        plan.Price = request.Price;
        plan.DurationMonths = request.DurationMonths;
        plan.IsActive = request.IsActive;
        plan.UpdatedAtUtc = DateTime.UtcNow;
        plan.UpdatedBy = actorId;

        await _membershipPlans.UpdateAsync(plan).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Updated membership plan {PlanId}.", id);

        return Result<MembershipPlanDetailDto>.Success(plan.ToDetailDto());
    }

    public async Task<Result> DeleteAsync(Guid id, Guid actorId, CancellationToken ct = default)
    {
        var plan = await _membershipPlans.GetForUpdateAsync(id, ct).ConfigureAwait(false);
        if (plan is null)
        {
            return Result.Failure(new Error(Error.Codes.NotFound, "Membership plan not found."));
        }

        if (!plan.IsActive)
        {
            return Result.Success();
        }

        plan.IsActive = false;
        plan.UpdatedAtUtc = DateTime.UtcNow;
        plan.UpdatedBy = actorId;

        await _membershipPlans.UpdateAsync(plan).ConfigureAwait(false);
        await _uow.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Deactivated membership plan {PlanId}.", id);

        return Result.Success();
    }
}

