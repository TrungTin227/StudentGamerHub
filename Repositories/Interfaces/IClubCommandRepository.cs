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
}
