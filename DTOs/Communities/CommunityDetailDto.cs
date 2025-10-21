namespace DTOs.Communities;

/// <summary>
/// Detailed information about a community.
/// </summary>
/// <param name="Id">Community identifier.</param>
/// <param name="Name">Community name.</param>
/// <param name="Description">Optional description.</param>
/// <param name="School">Optional associated school.</param>
/// <param name="IsPublic">Whether the community is public.</param>
/// <param name="MembersCount">Current cached members count.</param>
public sealed record CommunityDetailDto(
    Guid Id,
    string Name,
    string? Description,
    string? School,
    bool IsPublic,
    int MembersCount,
    int ClubsCount,
    Guid OwnerId,
    bool IsMember,
    bool IsOwner,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);
