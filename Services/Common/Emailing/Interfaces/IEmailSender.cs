namespace Services.Common.Emailing.Interfaces
{
    public interface IEmailSender
    {
        Task SendAsync(EmailMessage message, CancellationToken ct = default);
    }
}
