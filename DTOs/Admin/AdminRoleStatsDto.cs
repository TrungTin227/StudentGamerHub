namespace DTOs.Admin;

/// <summary>
/// Thống kê roles trong hệ thống
/// </summary>
public record AdminRoleStatsDto
{
    public Guid RoleId { get; init; }
    public string RoleName { get; init; } = string.Empty;

    /// <summary>
    /// Số users có role này
    /// </summary>
    public int UsersCount { get; init; }

    public DateTime CreatedAtUtc { get; init; }
}
