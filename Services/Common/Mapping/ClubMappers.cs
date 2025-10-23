using Repositories.Models;

namespace Services.Common.Mapping;

/// <summary>
/// Mapping extensions for Club entity to DTOs.
/// </summary>
public static class ClubMappers
{
    /// <summary>
    /// Maps Club entity to ClubBriefDto.
    /// </summary>
    public static ClubBriefDto ToClubBriefDto(this Club club)
    {
        ArgumentNullException.ThrowIfNull(club);

        return new ClubBriefDto(
            Id: club.Id,
            CommunityId: club.CommunityId,
            Name: club.Name,
            IsPublic: club.IsPublic,
            MembersCount: club.MembersCount,
            Description: club.Description
        );
    }

    /// <summary>
    /// Maps Club entity to ClubDetailDto.
    /// </summary>
    public static ClubDetailDto ToClubDetailDto(this ClubDetailModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new ClubDetailDto(
            model.Id,
            model.CommunityId,
            model.Name,
            model.Description,
            model.IsPublic,
            model.MembersCount,
            model.RoomsCount,
            model.OwnerId,
            model.IsMember,
            model.IsCommunityMember,
            model.IsOwner,
            model.CreatedAtUtc,
            model.UpdatedAtUtc
        );
    }
}
