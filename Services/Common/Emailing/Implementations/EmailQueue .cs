using System.Threading.Channels;

namespace Services.Common.Emailing.Implementations;

public sealed class EmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _channel =
        Channel.CreateBounded<EmailMessage>(new BoundedChannelOptions(500) { SingleReader = true, SingleWriter = false });

    public ValueTask EnqueueAsync(EmailMessage message, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(message, ct);

    internal ChannelReader<EmailMessage> Reader => _channel.Reader;
}
