using Services.DTOs.Memberships;

namespace Services.Interfaces;

public interface IMembershipReadService
{
    Task<Result<ClubRoomTreeHybridDto>> GetMyClubRoomTreeAsync(Guid userId, CancellationToken ct = default);
}
