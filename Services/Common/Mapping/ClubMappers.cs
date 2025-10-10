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
}
