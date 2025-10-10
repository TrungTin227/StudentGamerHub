namespace Services.Common.Mapping;

/// <summary>
/// Mapping helpers for room entities and DTOs.
/// </summary>
public static class RoomMappers
{
    /// <summary>
    /// Maps <see cref="Room"/> to <see cref="RoomDetailDto"/>.
    /// </summary>
    public static RoomDetailDto ToRoomDetailDto(this Room room)
    {
        ArgumentNullException.ThrowIfNull(room);

        return new RoomDetailDto(
            room.Id,
            room.ClubId,
            room.Name,
            room.Description,
            room.JoinPolicy,
            room.Capacity,
            room.MembersCount);
    }

    /// <summary>
    /// Maps <see cref="RoomMember"/> to <see cref="RoomMemberBriefDto"/>.
    /// </summary>
    public static RoomMemberBriefDto ToRoomMemberBriefDto(this RoomMember member)
    {
        ArgumentNullException.ThrowIfNull(member);

        return new RoomMemberBriefDto(
            member.UserId,
            member.User?.FullName ?? string.Empty,
            member.Role,
            member.Status,
            member.JoinedAt);
    }
}
