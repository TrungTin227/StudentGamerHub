using System.Text.Json.Serialization;

namespace DTOs.Payments.PayOs;

public sealed record PayOsWebhookPayload
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = "";

    [JsonPropertyName("desc")]
    public string Desc { get; init; } = "";

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("data")]
    public PayOsWebhookData Data { get; init; } = new();

    [JsonPropertyName("signature")]
    public string Signature { get; init; } = "";
}

public sealed record PayOsWebhookData
{
    [JsonPropertyName("orderCode")]
    public long OrderCode { get; init; }

    [JsonPropertyName("amount")]
    public long Amount { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    [JsonPropertyName("reference")]
    public string? Reference { get; init; }

    [JsonPropertyName("transactionDateTime")]
    public string TransactionDateTime { get; init; } = "";

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = "VND";

    [JsonPropertyName("paymentLinkId")]
    public string? PaymentLinkId { get; init; }

    // optional banking fields
    [JsonPropertyName("accountNumber")]
    public string? AccountNumber { get; init; }

    [JsonPropertyName("counterAccountBankId")]
    public string? CounterAccountBankId { get; init; }

    [JsonPropertyName("counterAccountBankName")]
    public string? CounterAccountBankName { get; init; }

    [JsonPropertyName("counterAccountName")]
    public string? CounterAccountName { get; init; }

    [JsonPropertyName("counterAccountNumber")]
    public string? CounterAccountNumber { get; init; }

    [JsonPropertyName("virtualAccountName")]
    public string? VirtualAccountName { get; init; }

    [JsonPropertyName("virtualAccountNumber")]
    public string? VirtualAccountNumber { get; init; }
}
