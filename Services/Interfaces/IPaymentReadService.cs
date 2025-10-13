namespace Services.Interfaces;

public interface IPaymentReadService
{
    Task<Result<PaymentIntentDto>> GetAsync(Guid userId, Guid paymentIntentId, CancellationToken ct = default);
}
