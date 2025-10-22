namespace DTOs.Payments.PayOs;

public sealed record PayOsWebhookPayload
{
    public string Code { get; init; } = "";
    public string Desc { get; init; } = "";
    public bool Success { get; init; }
    public PayOsWebhookData Data { get; init; } = new();
    public string Signature { get; init; } = "";
}

public sealed record PayOsWebhookData
{
    public long OrderCode { get; init; }                 // long
    public long Amount { get; init; }                    // VND integer
    public string Description { get; init; } = "";
    public string? Reference { get; init; }              // ưu tiên làm providerRef
    public string TransactionDateTime { get; init; } = "";
    public string Currency { get; init; } = "VND";
    public string? PaymentLinkId { get; init; }
    // optional banking fields (để phòng mở rộng)
    public string? AccountNumber { get; init; }
    public string? CounterAccountBankId { get; init; }
    public string? CounterAccountBankName { get; init; }
    public string? CounterAccountName { get; init; }
    public string? CounterAccountNumber { get; init; }
    public string? VirtualAccountName { get; init; }
    public string? VirtualAccountNumber { get; init; }
}