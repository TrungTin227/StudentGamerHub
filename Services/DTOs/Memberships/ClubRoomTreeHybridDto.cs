namespace Services.DTOs.Memberships;

public sealed class ClubRoomTreeHybridDto
{
    public IReadOnlyList<ClubNode> Clubs { get; private init; } = Array.Empty<ClubNode>();
    public OverviewHybrid Overview { get; private init; } = new(0, 0);

    public sealed record ClubNode(Guid ClubId, string? ClubName, IReadOnlyList<RoomNode> Rooms);
    public sealed record RoomNode(Guid RoomId, string? RoomName);
    public sealed record OverviewHybrid(int ClubCount, int RoomCount);

    public static ClubRoomTreeHybridDto FromBuilder(List<ClubNode> clubs, OverviewHybrid overview)
        => new() { Clubs = clubs, Overview = overview };
}
