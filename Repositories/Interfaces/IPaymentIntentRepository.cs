namespace Repositories.Interfaces;

public interface IPaymentIntentRepository
{
    Task<PaymentIntent?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PaymentIntent?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<PaymentIntent?> GetByProviderRefAsync(string providerRef, CancellationToken ct = default);
    Task CreateAsync(PaymentIntent pi, CancellationToken ct = default);
    Task UpdateAsync(PaymentIntent pi, CancellationToken ct = default);
    Task<int> CountActivePendingByEventAsync(Guid eventId, DateTime nowUtc, CancellationToken ct = default);
    Task<PaymentIntent?> GetByOrderCodeAsync(long orderCode, CancellationToken ct = default);
    Task<bool> TrySetOrderCodeAsync(Guid id, long orderCode, CancellationToken ct = default);
}
