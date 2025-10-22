namespace Services.DTOs.Memberships;

public sealed class MembershipTreeHybridDto
{
    public IReadOnlyList<CommunityNode> Communities { get; private init; } = Array.Empty<CommunityNode>();
    public OverviewHybrid Overview { get; private init; } = new(0, 0, 0);

    public sealed record OverviewHybrid(int CommunityCount, int ClubCount, int RoomCount);
    public sealed record CommunityNode(Guid CommunityId, string? CommunityName, IReadOnlyList<ClubNode> Clubs);
    public sealed record ClubNode(Guid ClubId, string? ClubName, IReadOnlyList<RoomNode> Rooms);
    public sealed record RoomNode(Guid RoomId, string? RoomName);

    public static MembershipTreeHybridDto FromBuilder(List<CommunityNode> communities, OverviewHybrid overview)
        => new()
        {
            Communities = communities,
            Overview = overview
        };
}
