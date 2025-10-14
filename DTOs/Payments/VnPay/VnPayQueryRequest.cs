namespace DTOs.Payments.VnPay;

public sealed record VnPayQueryRequest
{
    public string vnp_TxnRef { get; set; } = default!;
    public string vnp_OrderInfo { get; set; } = default!;
    public string vnp_TransDate { get; set; } = default!;
    public string vnp_CreateDate { get; set; } = default!;
    public string vnp_IpAddr { get; set; } = default!;
}
