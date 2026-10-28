using Identity.Api.Common.Options;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Identity.Api.Email;

public interface IEmailSender
{
    Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public sealed class MailKitEmailSender(
    IOptions<SmtpOptions> smtpOptions,
    ILogger<MailKitEmailSender> logger)
    : IEmailSender
{
    private readonly SmtpOptions _options = smtpOptions.Value;

    public async Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var mimeMessage = new MimeMessage();
            mimeMessage.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
            mimeMessage.To.Add(new MailboxAddress(message.ToEmail, message.ToEmail));
            mimeMessage.Subject = message.Subject;

            var bodyBuilder = new BodyBuilder
            {
                TextBody = message.PlainTextContent,
                HtmlBody = message.HtmlContent
            };
            mimeMessage.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_options.Host, _options.Port, _options.UseSsl, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.Username) && !string.IsNullOrWhiteSpace(_options.Password))
            {
                await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            }

            await client.SendAsync(mimeMessage, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            logger.LogInformation("Email sent successfully to {ToEmail}: {Subject}", message.ToEmail, message.Subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {ToEmail}: {Subject}", message.ToEmail, message.Subject);
        }
    }
}
