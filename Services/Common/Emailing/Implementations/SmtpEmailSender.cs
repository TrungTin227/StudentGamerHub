using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Services.Common.Emailing.Interfaces;

namespace Services.Common.Emailing.Implementations;

public sealed class SmtpEmailSender(IOptions<EmailOptions> opt) : IEmailSender
{
    private readonly EmailOptions _opt = opt.Value;

    public async Task SendAsync(EmailMessage msg, CancellationToken ct = default)
    {
        var mime = ToMimeMessage(msg, _opt);

        using var client = new SmtpClient();
        client.Timeout = _opt.Smtp.TimeoutSeconds * 1000;

        var secure = _opt.Smtp.UseSsl ? SecureSocketOptions.SslOnConnect
                  : _opt.Smtp.UseStartTls ? SecureSocketOptions.StartTls
                  : SecureSocketOptions.Auto;

        await client.ConnectAsync(_opt.Smtp.Host, _opt.Smtp.Port, secure, ct);

        if (!string.IsNullOrWhiteSpace(_opt.Smtp.User))
        {
            await client.AuthenticateAsync(_opt.Smtp.User, _opt.Smtp.Password, ct);
        }

        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(true, ct);
    }

    private static MimeMessage ToMimeMessage(EmailMessage m, EmailOptions o)
    {
        var mm = new MimeMessage();

        var from = m.From ?? new EmailAddress(o.DefaultFrom, o.DefaultFromName);
        mm.From.Add(new MailboxAddress(from.DisplayName ?? from.Address, from.Address));

        if (m.ReplyTo is not null)
            mm.ReplyTo.Add(new MailboxAddress(m.ReplyTo.DisplayName ?? m.ReplyTo.Address, m.ReplyTo.Address));

        foreach (var a in m.To) mm.To.Add(new MailboxAddress(a.DisplayName ?? a.Address, a.Address));
        foreach (var a in m.Cc) mm.Cc.Add(new MailboxAddress(a.DisplayName ?? a.Address, a.Address));
        foreach (var a in m.Bcc) mm.Bcc.Add(new MailboxAddress(a.DisplayName ?? a.Address, a.Address));

        mm.Subject = m.Subject;

        var builder = new BodyBuilder
        {
            TextBody = m.TextBody,
            HtmlBody = m.HtmlBody
        };

        foreach (var att in m.Attachments)
            builder.Attachments.Add(att.FileName, att.Content, ContentType.Parse(att.ContentType));

        mm.Body = builder.ToMessageBody();
        return mm;
    }
}
