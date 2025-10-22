namespace DTOs.Payments.PayOs;

public sealed record PayOsWebhookPayload
{
    public string Event { get; init; } = string.Empty;
    public PayOsWebhookData Data { get; init; } = new();

    public sealed record PayOsWebhookData
    {
        public string OrderCode { get; init; } = string.Empty;
        public long Amount { get; init; }
        public string Status { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string? TransactionId { get; init; }
        public string? PaymentLinkId { get; init; }
        public string? FailureReason { get; init; }
    }
}
