namespace Repositories.Interfaces;

/// <summary>
/// Command interface for community write operations.
/// </summary>
public interface ICommunityCommandRepository
{
    /// <summary>
    /// Create a new community.
    /// </summary>
    Task CreateAsync(Community community, CancellationToken ct = default);

    /// <summary>
    /// Update an existing community.
    /// </summary>
    Task UpdateAsync(Community community, CancellationToken ct = default);

    /// <summary>
    /// Soft delete (archive) a community.
    /// </summary>
    Task SoftDeleteAsync(Guid communityId, Guid deletedBy, CancellationToken ct = default);

    /// <summary>
    /// Add a community member.
    /// </summary>
    Task AddMemberAsync(CommunityMember member, CancellationToken ct = default);

    /// <summary>
    /// Remove a community member.
    /// </summary>
    Task RemoveMemberAsync(Guid communityId, Guid userId, CancellationToken ct = default);
}
