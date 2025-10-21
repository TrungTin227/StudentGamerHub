namespace Services.Interfaces;

/// <summary>
/// Service for managing communities.
/// </summary>
public interface ICommunityService
{
    Task<Result<CommunityDetailDto>> CreateCommunityAsync(CommunityCreateRequestDto req, Guid currentUserId, CancellationToken ct = default);
    Task<Result<CommunityDetailDto>> JoinCommunityAsync(Guid communityId, Guid currentUserId, CancellationToken ct = default);
    Task<Result> KickCommunityMemberAsync(Guid communityId, Guid targetUserId, Guid actorUserId, CancellationToken ct = default);
    Task<Result<CommunityDetailDto>> GetByIdAsync(Guid communityId, Guid? currentUserId = null, CancellationToken ct = default);
}
