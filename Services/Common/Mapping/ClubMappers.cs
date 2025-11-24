using Repositories.Models;

namespace Services.Common.Mapping;

/// <summary>
/// Mapping extensions for Club entity to DTOs.
/// </summary>
public static class ClubMappers
{
    /// <summary>
    /// Maps Club entity to ClubBriefDto (without IsJoined).
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
            Description: club.Description,
            IsJoined: false
        );
    }

    /// <summary>
    /// Maps ClubBriefModel to ClubBriefDto.
    /// </summary>
    public static ClubBriefDto ToClubBriefDto(this ClubBriefModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new ClubBriefDto(
            Id: model.Id,
            CommunityId: model.CommunityId,
            Name: model.Name,
            IsPublic: model.IsPublic,
            MembersCount: model.MembersCount,
            Description: model.Description,
            IsJoined: model.IsJoined
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
