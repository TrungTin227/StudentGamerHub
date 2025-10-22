namespace Services.Configuration;

public sealed class PayOsConfig
{
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChecksumKey { get; set; } = string.Empty; // aka SecretKey
    public string BaseUrl { get; set; } = "https://api-merchant.payos.vn"; // v2 domain
    public string ReturnUrl { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;  // optional (không bắt buộc cho create)
    public string? CancelUrl { get; set; }
}
