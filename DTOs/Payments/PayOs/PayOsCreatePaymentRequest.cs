namespace DTOs.Payments.PayOs;

public sealed record PayOsCreatePaymentRequest
{
    public required string OrderCode { get; init; }
    public required long Amount { get; init; }
    public string Currency { get; init; } = "VND";
    public string Description { get; init; } = string.Empty;
    public string ReturnUrl { get; init; } = string.Empty;
    public string CancelUrl { get; init; } = string.Empty;
    public string? BuyerName { get; init; }
    public string? BuyerEmail { get; init; }
    public string? BuyerPhone { get; init; }
}
