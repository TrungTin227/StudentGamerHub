using System.Globalization;
using BusinessObjects;
using Repositories.Interfaces;

namespace Services.Implementations;

/// <summary>
/// Read operations for payment intents.
/// </summary>
public sealed class PaymentReadService : IPaymentReadService
{
    private const string Provider = "PAYOS";

    private readonly IPaymentIntentRepository _paymentIntentRepository;
    private readonly ITransactionRepository _transactionRepository;

    public PaymentReadService(IPaymentIntentRepository paymentIntentRepository, ITransactionRepository transactionRepository)
    {
        _paymentIntentRepository = paymentIntentRepository ?? throw new ArgumentNullException(nameof(paymentIntentRepository));
        _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
    }

    public async Task<Result<PaymentIntentDto>> GetAsync(Guid userId, Guid paymentIntentId, CancellationToken ct = default)
    {
        var pi = await _paymentIntentRepository.GetByIdForUserAsync(paymentIntentId, userId, ct).ConfigureAwait(false);
        if (pi is null)
        {
            return Result<PaymentIntentDto>.Failure(new Error(Error.Codes.NotFound, "Payment intent not found."));
        }

        var transaction = await ResolveTransactionAsync(pi, ct).ConfigureAwait(false);
        var metadataJson = transaction?.Metadata?.RootElement.GetRawText();
        var transactionReference = transaction?.ProviderRef
                                   ?? (pi.OrderCode?.ToString(CultureInfo.InvariantCulture));
        var providerName = transaction?.Provider ?? (transactionReference is not null ? Provider : null);

        var dto = new PaymentIntentDto(
            pi.Id,
            pi.AmountCents,
            pi.Purpose,
            pi.EventRegistrationId,
            pi.EventId,
            pi.Status,
            pi.ExpiresAt,
            pi.ClientSecret,
            providerName,
            transactionReference,
            metadataJson);

        return Result<PaymentIntentDto>.Success(dto);
    }

    public async Task<Result<Guid>> ResolveIntentIdByOrderCodeAsync(long orderCode, CancellationToken ct = default)
    {
        if (orderCode <= 0)
        {
            return Result<Guid>.Failure(new Error(Error.Codes.Validation, "Order code must be positive."));
        }

        var pi = await _paymentIntentRepository.GetByOrderCodeAsync(orderCode, ct).ConfigureAwait(false);
        if (pi is null)
        {
            return Result<Guid>.Failure(new Error(Error.Codes.NotFound, "Payment intent not found."));
        }

        return Result<Guid>.Success(pi.Id);
    }

    private async Task<Transaction?> ResolveTransactionAsync(PaymentIntent pi, CancellationToken ct)
    {
        if (pi.EventRegistration?.PaidTransaction is not null)
        {
            return pi.EventRegistration.PaidTransaction;
        }

        if (!pi.OrderCode.HasValue)
        {
            return null;
        }

        var providerRef = pi.OrderCode.Value.ToString(CultureInfo.InvariantCulture);
        return await _transactionRepository.GetByProviderRefAsync(Provider, providerRef, ct).ConfigureAwait(false);
    }
}
