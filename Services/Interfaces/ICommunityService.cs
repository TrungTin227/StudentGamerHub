namespace Services.Interfaces;

/// <summary>
/// Service for managing communities.
/// </summary>
public interface ICommunityService
{
    Task<Result<Guid>> CreateAsync(Guid currentUserId, CommunityCreateRequestDto req, CancellationToken ct = default);
    Task<Result<CommunityDetailDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result> UpdateAsync(Guid currentUserId, Guid id, CommunityUpdateRequestDto req, CancellationToken ct = default);
    Task<Result> ArchiveAsync(Guid currentUserId, Guid id, CancellationToken ct = default);
}
