namespace Services.Configuration;

/// <summary>
/// VNPAY payment gateway configuration.
/// </summary>
public sealed class VnPayConfig
{
    public string TmnCode { get; set; } = default!;
    public string HashSecret { get; set; } = default!;
    public string BaseUrl { get; set; } = default!;
    public string ApiUrl { get; set; } = default!;
    public string ReturnUrl { get; set; } = default!;
    public string? ReturnUrlOrder { get; set; }
    public string? ReturnUrlCustomDesign { get; set; }
    public string Version { get; set; } = "2.1.0";
    public string CurrCode { get; set; } = "VND";
    public string Locale { get; set; } = "vn";
}
