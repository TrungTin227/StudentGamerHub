namespace DTOs.Communities;

/// <summary>
/// Response for discovery endpoint with cursor-based pagination.
/// </summary>
public sealed class DiscoverResponse
{
    public IReadOnlyList<CommunityDiscoverDto> Items { get; set; } = Array.Empty<CommunityDiscoverDto>();
    
    /// <summary>
    /// Cursor token for the next page of results. Null if no more pages.
    /// </summary>
    public string? NextCursor { get; set; }
}
