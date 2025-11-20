namespace Repositories.Interfaces;

public interface IEscrowRepository
{
    Task<Escrow?> GetByEventIdAsync(Guid eventId, CancellationToken ct = default);
    Task<Dictionary<Guid, Escrow>> GetByEventIdsAsync(IEnumerable<Guid> eventIds, CancellationToken ct = default);
    Task UpsertAsync(Escrow escrow, CancellationToken ct = default);
}
