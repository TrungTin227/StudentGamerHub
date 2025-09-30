namespace Services.Common.Emailing.Interfaces
{
    public interface IEmailQueue
    {
        ValueTask EnqueueAsync(EmailMessage message, CancellationToken ct = default);
    }
}
