using BusinessObjects;

namespace Repositories.Interfaces;

public interface ITransactionRepository
{
    Task CreateAsync(Transaction tx, CancellationToken ct = default);
    Task<bool> ExistsByProviderRefAsync(string provider, string providerRef, CancellationToken ct = default);
    Task<Transaction?> GetByProviderRefAsync(string provider, string providerRef, CancellationToken ct = default);
}
