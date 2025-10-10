namespace DTOs.Communities;

/// <summary>
/// Request payload to update a community.
/// </summary>
/// <param name="Name">Community name (required).</param>
/// <param name="Description">Optional description.</param>
/// <param name="School">Optional associated school.</param>
/// <param name="IsPublic">Whether the community is public.</param>
public sealed record CommunityUpdateRequestDto(
    string Name,
    string? Description,
    string? School,
    bool IsPublic
);
