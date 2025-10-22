using Services.DTOs.Memberships;

namespace Services.Common.Mapping;

public static class MembershipsProfile
{
    public static MembershipTreeMutableDto ToMutableDto(this MembershipTreeProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var dto = new MembershipTreeMutableDto
        {
            Overview = new MembershipTreeMutableDto.OverviewMutable
            {
                CommunityCount = projection.Overview.CommunityCount,
                ClubCount = projection.Overview.ClubCount,
                RoomCount = projection.Overview.RoomCount
            }
        };

        dto.Communities = projection.Communities
            .Select(community => new MembershipTreeMutableDto.CommunityNode
            {
                CommunityId = community.CommunityId,
                CommunityName = community.CommunityName,
                Clubs = community.Clubs
                    .Select(club => new MembershipTreeMutableDto.ClubNode
                    {
                        ClubId = club.ClubId,
                        ClubName = club.ClubName,
                        Rooms = club.Rooms
                            .Select(room => new MembershipTreeMutableDto.RoomNode
                            {
                                RoomId = room.RoomId,
                                RoomName = room.RoomName
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .ToList();

        return dto;
    }

    public static MembershipTreeImmutableDto ToImmutableDto(this MembershipTreeProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var communities = projection.Communities
            .Select(community => new MembershipTreeImmutableDto.CommunityNode(
                community.CommunityId,
                community.CommunityName,
                community.Clubs
                    .Select(club => new MembershipTreeImmutableDto.ClubNode(
                        club.ClubId,
                        club.ClubName,
                        club.Rooms
                            .Select(room => new MembershipTreeImmutableDto.RoomNode(room.RoomId, room.RoomName))
                            .ToList()))
                    .ToList()))
            .ToList();

        var overview = new MembershipTreeImmutableDto.OverviewImmutable(
            projection.Overview.CommunityCount,
            projection.Overview.ClubCount,
            projection.Overview.RoomCount);

        return new MembershipTreeImmutableDto(communities, overview);
    }

    public static MembershipTreeHybridDto ToHybridDto(this MembershipTreeProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var communities = projection.Communities
            .Select(community => new MembershipTreeHybridDto.CommunityNode(
                community.CommunityId,
                community.CommunityName,
                community.Clubs
                    .Select(club => new MembershipTreeHybridDto.ClubNode(
                        club.ClubId,
                        club.ClubName,
                        club.Rooms
                            .Select(room => new MembershipTreeHybridDto.RoomNode(room.RoomId, room.RoomName))
                            .ToList()))
                    .ToList()))
            .ToList();

        var overview = new MembershipTreeHybridDto.OverviewHybrid(
            projection.Overview.CommunityCount,
            projection.Overview.ClubCount,
            projection.Overview.RoomCount);

        return MembershipTreeHybridDto.FromBuilder(communities, overview);
    }
}

public sealed record MembershipTreeProjection(
    IReadOnlyList<MembershipCommunityProjection> Communities,
    MembershipOverviewProjection Overview);

public sealed record MembershipCommunityProjection(
    Guid CommunityId,
    string? CommunityName,
    IReadOnlyList<MembershipClubProjection> Clubs);

public sealed record MembershipClubProjection(
    Guid ClubId,
    string? ClubName,
    IReadOnlyList<MembershipRoomProjection> Rooms);

public sealed record MembershipRoomProjection(
    Guid RoomId,
    string? RoomName);

public sealed record MembershipOverviewProjection(
    int CommunityCount,
    int ClubCount,
    int RoomCount);
