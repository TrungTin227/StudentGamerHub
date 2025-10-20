using System.ComponentModel.DataAnnotations;

namespace Services.Common.Emailing;

public sealed class EmailOptions
{
    public const string Section = "Email";

    [Required, RegularExpression("Smtp|File|Resend")]
    public string Provider { get; init; } = "Smtp";

    [Required, EmailAddress]
    public string DefaultFrom { get; init; } = default!;

    [Required]
    public string DefaultFromName { get; init; } = default!;

    public FilePickupOptions File { get; init; } = new();
    public SmtpOptions Smtp { get; init; } = new();
}

public sealed class FilePickupOptions
{
    [Required]
    public string PickupDirectory { get; init; } = "App_Data/mails";
}

public sealed class SmtpOptions
{
    [Required] public string Host { get; init; } = default!;
    [Range(1, 65535)] public int Port { get; init; } = 587;
    public bool UseStartTls { get; init; } = true;
    public bool UseSsl { get; init; } = false;

    public string? User { get; init; }
    public string? Password { get; init; }

    [Range(5, 120)] public int TimeoutSeconds { get; init; } = 30;
}
