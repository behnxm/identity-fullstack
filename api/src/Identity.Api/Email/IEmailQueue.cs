using System.Threading.Channels;

namespace Identity.Api.Email;

public sealed record EmailMessage(
    string ToEmail,
    string Subject,
    string PlainTextContent,
    string HtmlContent);

public interface IEmailQueue
{
    ValueTask QueueEmailAsync(EmailMessage message, CancellationToken cancellationToken = default);
    IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken cancellationToken = default);
}

public sealed class EmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateUnbounded<EmailMessage>(
        new UnboundedChannelOptions { SingleReader = true });

    public ValueTask QueueEmailAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(message, cancellationToken);

    public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
