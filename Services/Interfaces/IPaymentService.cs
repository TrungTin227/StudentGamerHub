namespace Services.Interfaces;

public interface IPaymentService
{
    Task<Result> ConfirmAsync(Guid userId, Guid paymentIntentId, CancellationToken ct = default);
}
