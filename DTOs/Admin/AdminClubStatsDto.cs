namespace DTOs.Admin;

/// <summary>
/// Thống kê clubs cho Admin
/// </summary>
public record AdminClubStatsDto
{
    public Guid ClubId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsPublic { get; init; }

    public Guid CommunityId { get; init; }
    public string CommunityName { get; init; } = string.Empty;

    /// <summary>
    /// Số rooms trong club
    /// </summary>
    public int RoomsCount { get; init; }

    /// <summary>
    /// Số members
    /// </summary>
    public int MembersCount { get; init; }

    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public bool IsDeleted { get; init; }
}
