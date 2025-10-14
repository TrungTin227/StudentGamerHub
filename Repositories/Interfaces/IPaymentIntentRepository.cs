namespace Repositories.Interfaces;

public interface IPaymentIntentRepository
{
    Task<PaymentIntent?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PaymentIntent?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<PaymentIntent?> GetByProviderRefAsync(string providerRef, CancellationToken ct = default);
    Task CreateAsync(PaymentIntent pi, CancellationToken ct = default);
    Task UpdateAsync(PaymentIntent pi, CancellationToken ct = default);
}
