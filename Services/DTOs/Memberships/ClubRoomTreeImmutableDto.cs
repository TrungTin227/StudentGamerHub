namespace Services.DTOs.Memberships;

public sealed record ClubRoomTreeImmutableDto(
    IReadOnlyList<ClubRoomTreeImmutableDto.ClubNode> Clubs,
    ClubRoomTreeImmutableDto.OverviewImmutable Overview
)
{
    public sealed record ClubNode(Guid ClubId, string? ClubName, IReadOnlyList<RoomNode> Rooms);
    public sealed record RoomNode(Guid RoomId, string? RoomName);
    public sealed record OverviewImmutable(int ClubCount, int RoomCount);
}
