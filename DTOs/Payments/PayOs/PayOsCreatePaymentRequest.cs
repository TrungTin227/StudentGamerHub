namespace DTOs.Payments.PayOs;

public sealed record PayOsCreatePaymentRequest
{
    public required long OrderCode { get; init; }   // LONG (not Guid/string)
    public required long Amount { get; init; }      // VND (integer)
    public string Description { get; init; } = string.Empty;
    public string ReturnUrl { get; init; } = string.Empty;
    public string CancelUrl { get; init; } = string.Empty;
    public string? BuyerName { get; init; }
    public string? BuyerEmail { get; init; }
    public string? BuyerPhone { get; init; }
}
