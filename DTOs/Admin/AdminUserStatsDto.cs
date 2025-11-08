namespace DTOs.Admin;

/// <summary>
/// Thống kê chi tiết về user cho Admin
/// </summary>
public record AdminUserStatsDto
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? FullName { get; init; }
    public string? AvatarUrl { get; init; }
    public int Level { get; init; }
    public int Points { get; init; }

    /// <summary>
    /// Số dư ví (cents)
    /// </summary>
    public long WalletBalanceCents { get; init; }

    /// <summary>
    /// Membership hiện tại
    /// </summary>
    public string? CurrentMembership { get; init; }

    /// <summary>
    /// Ngày hết hạn membership
    /// </summary>
    public DateTime? MembershipExpiresAt { get; init; }

    /// <summary>
    /// Số events đã tạo
    /// </summary>
    public int EventsCreated { get; init; }

    /// <summary>
    /// Số events đã tham gia
    /// </summary>
    public int EventsAttended { get; init; }

    /// <summary>
    /// Số communities tham gia
    /// </summary>
    public int CommunitiesJoined { get; init; }

    /// <summary>
    /// Tổng số tiền đã chi tiêu (cents)
    /// </summary>
    public long TotalSpentCents { get; init; }

    /// <summary>
    /// Các roles
    /// </summary>
    public List<string> Roles { get; init; } = new();

    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public bool IsDeleted { get; init; }
}
