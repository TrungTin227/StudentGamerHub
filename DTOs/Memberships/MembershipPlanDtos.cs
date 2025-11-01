using System.ComponentModel.DataAnnotations;

namespace DTOs.Memberships;

public sealed record MembershipPlanSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    int MonthlyEventLimit,
    decimal Price,
    int DurationMonths,
    bool IsActive);

public sealed record MembershipPlanDetailDto(
    Guid Id,
    string Name,
    string? Description,
    int MonthlyEventLimit,
    decimal Price,
    int DurationMonths,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record MembershipPlanCreateRequest(
    [param: Required, MaxLength(128)] string Name,
    [param: MaxLength(1024)] string? Description,
    [param: Range(-1, int.MaxValue)] int MonthlyEventLimit,
    [param: Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal Price,
    [param: Range(1, 36)] int DurationMonths,
    bool IsActive = true);

public sealed record MembershipPlanUpdateRequest(
    [param: Required, MaxLength(128)] string Name,
    [param: MaxLength(1024)] string? Description,
    [param: Range(-1, int.MaxValue)] int MonthlyEventLimit,
    [param: Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal Price,
    [param: Range(1, 36)] int DurationMonths,
    bool IsActive);

public sealed record UserMembershipInfoDto(
    Guid MembershipPlanId,
    string PlanName,
    int MonthlyEventLimit,
    DateTime StartDate,
    DateTime EndDate,
    int? RemainingEventQuota,
    bool IsActive);

public sealed record MembershipPurchaseResultDto(
    bool RequiresExternalPayment,
    Guid? PaymentIntentId,
    UserMembershipInfoDto? Membership);
