namespace DTOs.Payments.VnPay;

public sealed record VnPayCreatePaymentResponse
{
    public bool Success { get; set; }
    public string PaymentUrl { get; set; } = default!;
    public string Message { get; set; } = default!;
}
