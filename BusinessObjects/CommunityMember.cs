using Microsoft.EntityFrameworkCore;
using BusinessObjects.Common;

namespace BusinessObjects;

/// <summary>
/// Membership link between a user and a community.
/// Composite primary key is configured via attribute.
/// </summary>
[PrimaryKey(nameof(CommunityId), nameof(UserId))]
public sealed class CommunityMember
{
    public Guid CommunityId { get; set; }
    public Community? Community { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public CommunityRole Role { get; set; } = CommunityRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Membership link between a user and a club within a community.
/// </summary>
[PrimaryKey(nameof(ClubId), nameof(UserId))]
public sealed class ClubMember
{
    public Guid ClubId { get; set; }
    public Club? Club { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public CommunityRole Role { get; set; } = CommunityRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
