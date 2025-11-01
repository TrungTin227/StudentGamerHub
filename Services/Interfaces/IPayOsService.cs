using DTOs.Payments.PayOs;

namespace Services.Interfaces;

public interface IPayOsService
{
    Task<Result<string>> CreatePaymentLinkAsync(PayOsCreatePaymentRequest req, CancellationToken ct = default);
    Task<Result<PayOsWebhookOutcome>> HandleWebhookAsync(PayOsWebhookPayload payload, string rawBody, string? signatureHeader, CancellationToken ct = default);
    bool VerifyWebhookSignature(PayOsWebhookPayload payload);
    Task<Result<PayOsPaymentInfo>> GetPaymentInfoAsync(long orderCode, CancellationToken ct = default);
}

public sealed record PayOsPaymentInfo
{
    public long OrderCode { get; init; }
    public long Amount { get; init; }
    public string Status { get; init; } = "";
    public string? Reference { get; init; }
    public string? TransactionDateTime { get; init; }
}
