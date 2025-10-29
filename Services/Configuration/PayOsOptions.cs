namespace Services.Configuration;

public sealed class PayOsOptions
{
    private string _secretKey = string.Empty;

    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Primary shared secret used for signing (aka checksum/webhook secret).
    /// </summary>
    public string SecretKey
    {
        get => _secretKey;
        set => _secretKey = value?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Back-compat binding for configuration key PayOS:ChecksumKey.
    /// </summary>
    public string ChecksumKey
    {
        get => SecretKey;
        set => SecretKey = value;
    }

    /// <summary>
    /// Alternate binding for configuration key PayOS:WebhookSecret.
    /// </summary>
    public string WebhookSecret
    {
        get => SecretKey;
        set => SecretKey = value;
    }

    public string BaseUrl { get; set; } = "https://api-merchant.payos.vn";
    public string ReturnUrl { get; set; } = string.Empty;
    public string WebhookUrl { get; set; } = string.Empty;
    public string? CancelUrl { get; set; }
    public string FrontendBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Maximum allowed skew for incoming webhook timestamps.
    /// </summary>
    public TimeSpan WebhookTolerance { get; set; } = TimeSpan.FromMinutes(5);
}
