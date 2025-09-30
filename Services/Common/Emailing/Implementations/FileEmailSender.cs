using Microsoft.Extensions.Options;
using MimeKit;
using Services.Common.Emailing;
using Services.Common.Emailing.Interfaces;

namespace Services.Common.Emailing.Implementations;

public sealed class FileEmailSender(IOptions<EmailOptions> opt) : IEmailSender
{
    private readonly EmailOptions _opt = opt.Value;

    public async Task SendAsync(EmailMessage msg, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_opt.File.PickupDirectory);

        var mime = new MimeMessage();
        var from = msg.From ?? new EmailAddress(_opt.DefaultFrom, _opt.DefaultFromName);
        mime.From.Add(new MailboxAddress(from.DisplayName ?? from.Address, from.Address));
        foreach (var a in msg.To)
            mime.To.Add(new MailboxAddress(a.DisplayName ?? a.Address, a.Address));
        mime.Subject = msg.Subject;

        var builder = new BodyBuilder { TextBody = msg.TextBody, HtmlBody = msg.HtmlBody };
        foreach (var att in msg.Attachments)
            builder.Attachments.Add(att.FileName, att.Content, ContentType.Parse(att.ContentType));
        mime.Body = builder.ToMessageBody();

        var file = Path.Combine(_opt.File.PickupDirectory, $"{DateTime.UtcNow:yyyyMMdd_HHmmssfff}_{Guid.NewGuid():N}.eml");
        await using var stream = File.Create(file);
        await mime.WriteToAsync(stream, ct);
    }
}
