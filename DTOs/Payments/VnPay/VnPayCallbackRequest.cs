namespace DTOs.Payments.VnPay;

/// <summary>
/// VNPAY callback request - includes all vnp_* fields from IPN/return URL.
/// </summary>
public sealed record VnPayCallbackRequest
{
    public long vnp_Amount { get; set; }
    public string? vnp_BankCode { get; set; }
    public string? vnp_BankTranNo { get; set; }
    public string? vnp_CardType { get; set; }
    public string? vnp_OrderInfo { get; set; }
    public string? vnp_PayDate { get; set; }
    public string vnp_ResponseCode { get; set; } = default!;
    public string? vnp_TmnCode { get; set; }
    public string? vnp_TransactionNo { get; set; }
    public string? vnp_TransactionStatus { get; set; }
    public string vnp_TxnRef { get; set; } = default!;
    public string vnp_SecureHash { get; set; } = default!;
    public string? vnp_SecureHashType { get; set; }
}
