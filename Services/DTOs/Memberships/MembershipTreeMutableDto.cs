namespace Services.DTOs.Memberships;

public sealed class MembershipTreeMutableDto
{
    public List<CommunityNode> Communities { get; set; } = new();
    public OverviewMutable Overview { get; set; } = new();

    public sealed class CommunityNode
    {
        public Guid CommunityId { get; set; }
        public string? CommunityName { get; set; }
        public List<ClubNode> Clubs { get; set; } = new();
    }

    public sealed class ClubNode
    {
        public Guid ClubId { get; set; }
        public string? ClubName { get; set; }
        public List<RoomNode> Rooms { get; set; } = new();
    }

    public sealed class RoomNode
    {
        public Guid RoomId { get; set; }
        public string? RoomName { get; set; }
    }

    public sealed class OverviewMutable
    {
        public int CommunityCount { get; set; }
        public int ClubCount { get; set; }
        public int RoomCount { get; set; }
    }
}
