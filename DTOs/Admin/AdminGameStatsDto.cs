namespace DTOs.Admin;

/// <summary>
/// Thống kê games cho Admin
/// </summary>
public record AdminGameStatsDto
{
    public Guid GameId { get; init; }
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Số users chơi game này
    /// </summary>
    public int PlayersCount { get; init; }

    /// <summary>
    /// Số communities liên kết
    /// </summary>
    public int CommunitiesCount { get; init; }

    public DateTime CreatedAtUtc { get; init; }
    public bool IsDeleted { get; init; }
}
