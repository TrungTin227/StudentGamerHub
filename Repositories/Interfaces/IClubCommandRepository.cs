namespace Repositories.Interfaces;

/// <summary>
/// Command interface for Club entity - write operations
/// Does NOT manage transactions - caller must use ExecuteTransactionAsync
/// </summary>
public interface IClubCommandRepository
{
    /// <summary>
    /// Create a new club.
    /// Does not commit - transaction is managed by UnitOfWork.
    /// </summary>
    /// <param name="club">Club entity to create</param>
    /// <param name="ct">Cancellation token</param>
    Task CreateAsync(Club club, CancellationToken ct = default);

    /// <summary>
    /// Update an existing club.
    /// </summary>
    /// <param name="club">Club entity with updated values</param>
    /// <param name="ct">Cancellation token</param>
    Task UpdateAsync(Club club, CancellationToken ct = default);

    /// <summary>
    /// Soft delete (archive) a club.
    /// </summary>
    /// <param name="clubId">Club identifier</param>
    /// <param name="deletedBy">User performing the deletion</param>
    /// <param name="ct">Cancellation token</param>
    Task SoftDeleteAsync(Guid clubId, Guid deletedBy, CancellationToken ct = default);

    /// <summary>
    /// Add a club member.
    /// </summary>
    Task AddMemberAsync(ClubMember member, CancellationToken ct = default);

    /// <summary>
    /// Remove a club member.
    /// </summary>
    Task RemoveMemberAsync(Guid clubId, Guid userId, CancellationToken ct = default);
}
