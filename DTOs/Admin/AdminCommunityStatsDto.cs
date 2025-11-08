namespace DTOs.Admin;

/// <summary>
/// Thống kê communities cho Admin
/// </summary>
public record AdminCommunityStatsDto
{
    public Guid CommunityId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? School { get; init; }
    public bool IsPublic { get; init; }
    public int CachedMembersCount { get; init; }

    /// <summary>
    /// Số clubs trong community
    /// </summary>
    public int ClubsCount { get; init; }

    /// <summary>
    /// Số events được tổ chức
    /// </summary>
    public int EventsCount { get; init; }

    /// <summary>
    /// Số members thực tế (từ CommunityMember table)
    /// </summary>
    public int ActualMembersCount { get; init; }

    /// <summary>
    /// Số games liên kết
    /// </summary>
    public int GamesCount { get; init; }

    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public bool IsDeleted { get; init; }
}
