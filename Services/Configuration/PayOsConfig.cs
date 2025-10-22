namespace Services.Configuration;

public sealed class PayOsConfig
{
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.payos.vn/v1";
    public string ReturnUrl { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;
    public string? CancelUrl { get; set; }
}
