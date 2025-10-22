using Xunit;

namespace Services.Payments.Tests;

public sealed class PayOsServiceTests
{
    [Fact(Skip = "Integration smoke test - requires PayOS sandbox credentials")]
    public void CreatePaymentLink_ReturnsCheckoutUrl()
    {
        // Arrange: seed payment intent, configure PayOS sandbox keys.
        // Act: call IPaymentService.CreateHostedCheckoutUrlAsync and capture response.
        // Assert: verify returned payload contains non-empty checkout URL.
    }

    [Fact(Skip = "Integration smoke test - requires PayOS webhook callback")]
    public void Webhook_SuccessCreditsWallet()
    {
        // Arrange: perform a sandbox payment flow and capture the webhook payload from PayOS.
        // Act: send payload + signature to POST /api/payments/payos/webhook.
        // Assert: confirm wallet balance increases and payment intent status becomes Succeeded.
    }

    [Fact(Skip = "Integration smoke test - invalid signature should be rejected")]
    public void Webhook_InvalidSignature_ReturnsForbidden()
    {
        // Arrange: craft a valid webhook payload but tamper with signature header.
        // Act: POST to /api/payments/payos/webhook with incorrect signature.
        // Assert: API responds with 403 Forbidden and payment intent remains unchanged.
    }
}
