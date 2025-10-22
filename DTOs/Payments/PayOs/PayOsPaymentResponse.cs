namespace DTOs.Payments.PayOs;

public sealed record PayOsPaymentResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public PayOsPaymentResponseData? Data { get; init; }

    public sealed record PayOsPaymentResponseData
    {
        public string? CheckoutUrl { get; init; }
        public string? OrderCode { get; init; }
        public string? PaymentLinkId { get; init; }
    }
}
