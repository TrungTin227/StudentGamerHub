namespace Services.Common.Emailing;

public sealed record EmailAddress(string Address, string? DisplayName = null);

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

public sealed class EmailMessage
{
    public List<EmailAddress> To { get; } = new();
    public List<EmailAddress> Cc { get; } = new();
    public List<EmailAddress> Bcc { get; } = new();

    public EmailAddress? From { get; set; }
    public EmailAddress? ReplyTo { get; set; }

    public string Subject { get; set; } = string.Empty;
    public string? TextBody { get; set; }
    public string? HtmlBody { get; set; }

    public List<EmailAttachment> Attachments { get; } = new();
}
