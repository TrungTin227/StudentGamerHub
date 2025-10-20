using System.ComponentModel.DataAnnotations;

namespace Services.Common.Emailing;

public sealed class ResendOptions
{
    public const string Section = "Resend";

    [Required]
    public string ApiKey { get; init; } = string.Empty;
}
