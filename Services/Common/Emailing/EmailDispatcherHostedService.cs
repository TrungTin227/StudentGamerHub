using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Services.Common.Emailing;

public sealed class EmailDispatcherHostedService(
    EmailQueue queue,
    IEmailSender sender,
    ILogger<EmailDispatcherHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var msg in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try { await sender.SendAsync(msg, stoppingToken); }
            catch (Exception ex) { logger.LogError(ex, "Send email failed: {Subject}", msg.Subject); }
        }
    }
}
