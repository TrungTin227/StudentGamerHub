namespace Services.Common.Mapping;

/// <summary>
/// Mappers for Community entity to DTOs.
/// </summary>
public static class CommunityMappers
{
    /// <summary>
    /// Map Community entity to brief DTO for search results.
    /// </summary>
    public static CommunityBriefDto ToBriefDto(this Community community)
    {
        ArgumentNullException.ThrowIfNull(community);

        return new CommunityBriefDto(
            Id: community.Id,
            Name: community.Name,
            School: community.School,
            IsPublic: community.IsPublic,
            MembersCount: community.MembersCount
        );
    }
}
