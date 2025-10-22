using Services.DTOs.Memberships;

namespace Services.Common.Mapping;

public static class MembershipsProfile
{
    public static ClubRoomTreeMutableDto ToMutableDto(this ClubRoomTreeProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var dto = new ClubRoomTreeMutableDto
        {
            Overview = new ClubRoomTreeMutableDto.OverviewMutable
            {
                ClubCount = projection.Overview.ClubCount,
                RoomCount = projection.Overview.RoomCount
            }
        };

        dto.Clubs = projection.Clubs
            .Select(club => new ClubRoomTreeMutableDto.ClubNode
            {
                ClubId = club.ClubId,
                ClubName = club.ClubName,
                Rooms = club.Rooms
                    .Select(room => new ClubRoomTreeMutableDto.RoomNode
                    {
                        RoomId = room.RoomId,
                        RoomName = room.RoomName
                    })
                    .ToList()
            })
            .ToList();

        return dto;
    }

    public static ClubRoomTreeImmutableDto ToImmutableDto(this ClubRoomTreeProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var clubs = projection.Clubs
            .Select(club => new ClubRoomTreeImmutableDto.ClubNode(
                club.ClubId,
                club.ClubName,
                club.Rooms
                    .Select(room => new ClubRoomTreeImmutableDto.RoomNode(room.RoomId, room.RoomName))
                    .ToList()))
            .ToList();

        var overview = new ClubRoomTreeImmutableDto.OverviewImmutable(
            projection.Overview.ClubCount,
            projection.Overview.RoomCount);

        return new ClubRoomTreeImmutableDto(clubs, overview);
    }

    public static ClubRoomTreeHybridDto ToHybridDto(this ClubRoomTreeProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        var clubs = projection.Clubs
            .Select(club => new ClubRoomTreeHybridDto.ClubNode(
                club.ClubId,
                club.ClubName,
                club.Rooms
                    .Select(room => new ClubRoomTreeHybridDto.RoomNode(room.RoomId, room.RoomName))
                    .ToList()))
            .ToList();

        var overview = new ClubRoomTreeHybridDto.OverviewHybrid(
            projection.Overview.ClubCount,
            projection.Overview.RoomCount);

        return ClubRoomTreeHybridDto.FromBuilder(clubs, overview);
    }
}

public sealed record ClubRoomTreeProjection(
    IReadOnlyList<ClubProjection> Clubs,
    ClubRoomOverviewProjection Overview);

public sealed record ClubProjection(
    Guid ClubId,
    string? ClubName,
    IReadOnlyList<RoomProjection> Rooms);

public sealed record RoomProjection(
    Guid RoomId,
    string? RoomName);

public sealed record ClubRoomOverviewProjection(
    int ClubCount,
    int RoomCount);
