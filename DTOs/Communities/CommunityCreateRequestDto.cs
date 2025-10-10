namespace DTOs.Communities;

/// <summary>
/// Request payload to create a community.
/// </summary>
/// <param name="IdIgnored">Optional identifier from client (ignored by server).</param>
/// <param name="Name">Community name (required).</param>
/// <param name="Description">Optional description.</param>
/// <param name="School">Optional associated school.</param>
/// <param name="IsPublic">Whether the community is public.</param>
public sealed record CommunityCreateRequestDto(
    Guid? IdIgnored,
    string Name,
    string? Description,
    string? School,
    bool IsPublic
);
