using Repositories.Models;

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

    /// <summary>
    /// Maps a community entity to its detailed DTO representation.
    /// </summary>
    public static CommunityDetailDto ToDetailDto(this CommunityDetailModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new CommunityDetailDto(
            model.Id,
            model.Name,
            model.Description,
            model.School,
            model.IsPublic,
            model.MembersCount,
            model.ClubsCount,
            model.OwnerId,
            model.IsMember,
            model.IsOwner,
            model.CreatedAtUtc,
            model.UpdatedAtUtc
        );
    }
}
