namespace Services.Interfaces;

public interface IPaymentService
{
    Task<Result<MembershipPurchaseResultDto>> BuyMembershipAsync(Guid userId, Guid planId, CancellationToken ct = default);
    Task<Result<Guid>> CreateWalletTopUpIntentAsync(Guid userId, long amountCents, CancellationToken ct = default);
    Task<Result> ConfirmAsync(Guid userId, Guid paymentIntentId, CancellationToken ct = default);
    Task<Result<string>> CreateHostedCheckoutUrlAsync(Guid userId, Guid paymentIntentId, string? returnUrl, string clientIp, CancellationToken ct = default);
}
