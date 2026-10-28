using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Identity.Api.Email;

/// <summary>
/// Background worker that drains the email queue asynchronously.
/// Decouples outbound network I/O from HTTP endpoints, ensuring constant-time anti-enumeration response characteristics.
/// </summary>
public sealed class EmailBackgroundService(
    IEmailQueue emailQueue,
    IEmailSender emailSender,
    ILogger<EmailBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Email background queue worker started.");

        try
        {
            await foreach (var message in emailQueue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await emailSender.SendEmailAsync(message, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error occurred processing email from queue.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Email background queue worker stopping.");
        }
    }
}
