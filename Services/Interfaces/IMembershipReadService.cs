using DTOs.Memberships;
using Services.DTOs.Memberships;

namespace Services.Interfaces;

public interface IMembershipReadService
{
    /// <summary>
    /// Gets the club/room membership tree for the specified user.
    /// </summary>
    Task<Result<ClubRoomTreeHybridDto>> GetMyClubRoomTreeAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets the user's current MembershipPlan membership with quota and expiry details.
    /// Returns null if the user has no membership or if it has expired.
    /// </summary>
    Task<Result<UserMembershipInfoDto?>> GetCurrentMembershipAsync(Guid userId, CancellationToken ct = default);
}
