namespace DTOs.Admin.Filters;

/// <summary>
/// Filter cho danh sách users (Admin view)
/// </summary>
public class AdminUserFilter
{
    /// <summary>
    /// Tìm kiếm theo username, email, hoặc full name
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// Lọc theo role
    /// </summary>
    public string? Role { get; set; }

    /// <summary>
    /// Membership plan name
    /// </summary>
    public string? MembershipPlan { get; set; }

    /// <summary>
    /// Có membership đang active không
    /// </summary>
    public bool? HasActiveMembership { get; set; }

    /// <summary>
    /// Bao gồm users đã xóa
    /// </summary>
    public bool IncludeDeleted { get; set; } = false;

    /// <summary>
    /// Tạo từ ngày
    /// </summary>
    public DateTime? CreatedFrom { get; set; }

    /// <summary>
    /// Tạo đến ngày
    /// </summary>
    public DateTime? CreatedTo { get; set; }

    /// <summary>
    /// Số dư tối thiểu (cents)
    /// </summary>
    public long? MinBalanceCents { get; set; }

    /// <summary>
    /// Số dư tối đa (cents)
    /// </summary>
    public long? MaxBalanceCents { get; set; }
}
