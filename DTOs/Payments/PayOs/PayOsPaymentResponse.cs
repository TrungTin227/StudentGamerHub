namespace DTOs.Payments.PayOs;

public sealed record PayOsPaymentResponse
{
    public string Code { get; init; } = "";     // "00" khi OK
    public string Desc { get; init; } = "";
    public bool Success { get; init; }
    public PayOsPaymentResponseData? Data { get; init; }
    public sealed record PayOsPaymentResponseData
    {
        public string? CheckoutUrl { get; init; }
        public long? OrderCode { get; init; }
        public string? PaymentLinkId { get; init; }
    }
}

