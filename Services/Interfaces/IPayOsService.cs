using DTOs.Payments.PayOs;

namespace Services.Interfaces;

public interface IPayOsService
{
    Task<Result<string>> CreatePaymentLinkAsync(PayOsCreatePaymentRequest req, CancellationToken ct = default);
    Task<Result> HandleWebhookAsync(PayOsWebhookPayload payload, string rawBody, string? signatureHeader, CancellationToken ct = default);
    bool VerifyChecksum(string rawBody, string signatureHeader);
}
