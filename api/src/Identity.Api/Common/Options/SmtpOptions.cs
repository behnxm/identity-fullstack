namespace Identity.Api.Common.Options;

/// <summary>
/// Configuration for outbound email delivery via SMTP (MailKit).
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "mailpit";
    public int Port { get; set; } = 1025;
    public string FromEmail { get; set; } = "noreply@identity.local";
    public string FromName { get; set; } = "Identity Fullstack Security";
    public bool UseSsl { get; set; } = false;
    public string? Username { get; set; }
    public string? Password { get; set; }
}
