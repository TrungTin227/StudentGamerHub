namespace DTOs.Communities;

/// <summary>
/// DTO for community discovery endpoint with popularity metrics.
/// Sorted by popularity: MembersCount DESC, RecentActivity48h DESC, CreatedAtUtc DESC, Id ASC.
/// </summary>
public sealed class CommunityDiscoverDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string? School { get; set; }
    public bool IsPublic { get; set; }
    public int MembersCount { get; set; }
    
    /// <summary>
    /// Number of room joins in the last 48 hours (approximate recent activity indicator).
    /// </summary>
    public int RecentActivity48h { get; set; }
    
    public DateTime CreatedAtUtc { get; set; }
}
