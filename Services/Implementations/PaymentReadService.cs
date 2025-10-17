namespace Services.Implementations;

/// <summary>
/// Read operations for payment intents.
/// </summary>
public sealed class PaymentReadService : IPaymentReadService
{
    private readonly IPaymentIntentRepository _paymentIntentRepository;

    public PaymentReadService(IPaymentIntentRepository paymentIntentRepository)
    {
        _paymentIntentRepository = paymentIntentRepository ?? throw new ArgumentNullException(nameof(paymentIntentRepository));
    }

    public async Task<Result<PaymentIntentDto>> GetAsync(Guid userId, Guid paymentIntentId, CancellationToken ct = default)
    {
        var pi = await _paymentIntentRepository.GetByIdForUserAsync(paymentIntentId, userId, ct).ConfigureAwait(false);
        if (pi is null)
        {
            return Result<PaymentIntentDto>.Failure(new Error(Error.Codes.NotFound, "Payment intent not found."));
        }

        var dto = new PaymentIntentDto(
            pi.Id,
            pi.AmountCents,
            pi.Purpose,
            pi.EventRegistrationId,
            pi.EventId,
            pi.Status,
            pi.ExpiresAt,
            pi.ClientSecret);

        return Result<PaymentIntentDto>.Success(dto);
    }
}
