using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Services.Implementations;

public sealed class MembershipPlanService : IMembershipPlanService
{
    private static readonly TimeSpan PlansCacheTtl = TimeSpan.FromMinutes(30);
    private const string AllPlansCacheKey = "membership:plans:all";
    private const string PublicPlansCacheKey = "membership:plans:public";

    private readonly IGenericUnitOfWork _uow;
    private readonly IMembershipPlanRepository _membershipPlans;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<MembershipPlanService> _logger;

    public MembershipPlanService(
        IGenericUnitOfWork uow,
        IMembershipPlanRepository membershipPlans,
        IMemoryCache memoryCache,
        ILogger<MembershipPlanService> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _membershipPlans = membershipPlans ?? throw new ArgumentNullException(nameof(membershipPlans));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<IReadOnlyList<MembershipPlanSummaryDto>>> GetAllAsync(bool includeInactive, CancellationToken ct = default)
    {
        // ✅ Cache key based on includeInactive flag
        var cacheKey = includeInactive ? $"{AllPlansCacheKey}:inactive" : AllPlansCacheKey;

        if (_memoryCache.TryGetValue<IReadOnlyList<MembershipPlanSummaryDto>>(cacheKey, out var cached))
        {
            _logger.LogDebug("Membership plans cache HIT for includeInactive: {IncludeInactive}", includeInactive);
            return Result<IReadOnlyList<MembershipPlanSummaryDto>>.Success(cached!);
        }

        var plans = await _membershipPlans.GetAllAsync(includeInactive, ct).ConfigureAwait(false);
        var dtos = plans.Select(p => p.ToSummaryDto()).ToList();

        // Cache for 30 minutes (membership plans rarely change)
        _memoryCache.Set(cacheKey, dtos, PlansCacheTtl);
        _logger.LogDebug("Membership plans cached for includeInactive: {IncludeInactive}", includeInactive);

        return Result<IReadOnlyList<MembershipPlanSummaryDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<MembershipPlanSummaryDto>>> GetPublicAsync(CancellationToken ct = default)
    {
        if (_memoryCache.TryGetValue<IReadOnlyList<MembershipPlanSummaryDto>>(PublicPlansCacheKey, out var cached))
        {
            _logger.LogDebug("Public membership plans cache HIT");
            return Result<IReadOnlyList<MembershipPlanSummaryDto>>.Success(cached!);
        }

        var plans = await _membershipPlans.GetAllAsync(includeInactive: false, ct).ConfigureAwait(false);
        var dtos = plans.Where(p => p.IsActive).Select(p => p.ToSummaryDto()).ToList();

        // Cache for 30 minutes
        _memoryCache.Set(PublicPlansCacheKey, dtos, PlansCacheTtl);
        _logger.LogDebug("Public membership plans cached");

        return Result<IReadOnlyList<MembershipPlanSummaryDto>>.Success(dtos);
    }

    public async Task<Result<MembershipPlanDetailDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        // ✅ Cache individual plan by ID
        var cacheKey = $"membership:plan:{id}";

        if (_memoryCache.TryGetValue<MembershipPlanDetailDto>(cacheKey, out var cached))
        {
            _logger.LogDebug("Membership plan cache HIT for id: {PlanId}", id);
            return Result<MembershipPlanDetailDto>.Success(cached!);
        }

        var plan = await _membershipPlans.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (plan is null)
        {
            return Result<MembershipPlanDetailDto>.Failure(new Error(Error.Codes.NotFound, "Membership plan not found."));
        }

        var dto = plan.ToDetailDto();

        // Cache for 30 minutes
        _memoryCache.Set(cacheKey, dto, PlansCacheTtl);
        _logger.LogDebug("Membership plan cached for id: {PlanId}", id);

        return Result<MembershipPlanDetailDto>.Success(dto);
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

        // ✅ Invalidate all related caches
        InvalidatePlanCaches();

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

        // ✅ Invalidate all related caches
        InvalidatePlanCaches();

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

        // ✅ Invalidate all related caches
        InvalidatePlanCaches();

        _logger.LogInformation("Deactivated membership plan {PlanId}.", id);

        return Result.Success();
    }

    private void InvalidatePlanCaches()
    {
        _memoryCache.Remove(AllPlansCacheKey);
        _memoryCache.Remove($"{AllPlansCacheKey}:inactive");
        _memoryCache.Remove(PublicPlansCacheKey);
        _logger.LogDebug("Invalidated all membership plan caches");
    }
}



