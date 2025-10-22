namespace Services.DTOs.Memberships;

public sealed record MembershipTreeImmutableDto(
    IReadOnlyList<CommunityNode> Communities,
    OverviewImmutable Overview
)
{
    public sealed record CommunityNode(Guid CommunityId, string? CommunityName, IReadOnlyList<ClubNode> Clubs);
    public sealed record ClubNode(Guid ClubId, string? ClubName, IReadOnlyList<RoomNode> Rooms);
    public sealed record RoomNode(Guid RoomId, string? RoomName);
    public sealed record OverviewImmutable(int CommunityCount, int ClubCount, int RoomCount);
}
