namespace DTOs.Payments.VnPay;

public sealed record VnPayCreatePaymentRequest
{
    public long vnp_Amount { get; set; }
    public string vnp_TxnRef { get; set; } = default!;
    public string vnp_OrderInfo { get; set; } = default!;
    public string vnp_OrderType { get; set; } = default!;
    public string vnp_Locale { get; set; } = default!;
    public string vnp_IpAddr { get; set; } = default!;
    public string vnp_CreateDate { get; set; } = default!;
    public string? vnp_BankCode { get; set; }
}
