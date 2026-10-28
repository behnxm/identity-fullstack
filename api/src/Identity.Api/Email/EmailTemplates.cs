using System.Net;

namespace Identity.Api.Email;

public static class EmailTemplates
{
    public static EmailMessage ConfirmEmail(string webBaseUrl, Guid userId, string token, string recipientEmail)
    {
        // URL fragment preserves secrecy: fragments are never sent to HTTP servers or leaked in Referer headers
        var link = $"{webBaseUrl.TrimEnd('/')}/confirm-email?userId={userId}#token={WebUtility.UrlEncode(token)}";
        var subject = "Confirm your email address";
        var plain = $"Welcome! Please confirm your email address by visiting: {link}\nThis link expires in 1 hour.";
        var html = $"""
            <div style="font-family: sans-serif; line-height: 1.5; color: #333;">
              <h2>Confirm your email address</h2>
              <p>Welcome to Identity Fullstack! Please confirm your account by clicking the button below:</p>
              <p style="margin: 24px 0;">
                <a href="{link}" style="background-color: #10b981; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold;">Confirm Email</a>
              </p>
              <p style="color: #666; font-size: 14px;">Or copy and paste this URL into your browser:<br/><a href="{link}">{link}</a></p>
              <p style="color: #999; font-size: 12px;">This link will expire in 1 hour.</p>
            </div>
            """;

        return new EmailMessage(recipientEmail, subject, plain, html);
    }

    public static EmailMessage AlreadyRegistered(string webBaseUrl, string recipientEmail)
    {
        var resetLink = $"{webBaseUrl.TrimEnd('/')}/forgot-password";
        var subject = "Registration attempt on your email";
        var plain = $"Someone recently attempted to register an account using this email address. If this was you, you can reset your password at: {resetLink}\nIf this was not you, no action is needed.";
        var html = $"""
            <div style="font-family: sans-serif; line-height: 1.5; color: #333;">
              <h2>Registration attempt</h2>
              <p>Someone recently attempted to register a new account using your email address.</p>
              <p>If you already have an account and forgot your password, you can reset it here:</p>
              <p style="margin: 24px 0;">
                <a href="{resetLink}" style="background-color: #3b82f6; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold;">Reset Password</a>
              </p>
              <p style="color: #666; font-size: 14px;">If you did not initiate this request, your account remains secure and no action is required.</p>
            </div>
            """;

        return new EmailMessage(recipientEmail, subject, plain, html);
    }

    public static EmailMessage ResetPassword(string webBaseUrl, Guid userId, string token, string recipientEmail)
    {
        var link = $"{webBaseUrl.TrimEnd('/')}/reset-password?userId={userId}#token={WebUtility.UrlEncode(token)}";
        var subject = "Reset your password";
        var plain = $"A password reset was requested for your account. Visit the following link to choose a new password: {link}\nThis link expires in 1 hour. If you did not make this request, please ignore this email.";
        var html = $"""
            <div style="font-family: sans-serif; line-height: 1.5; color: #333;">
              <h2>Reset your password</h2>
              <p>We received a request to reset your password. Click the button below to choose a new password:</p>
              <p style="margin: 24px 0;">
                <a href="{link}" style="background-color: #3b82f6; color: white; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold;">Reset Password</a>
              </p>
              <p style="color: #666; font-size: 14px;">Or copy and paste this URL into your browser:<br/><a href="{link}">{link}</a></p>
              <p style="color: #999; font-size: 12px;">This link will expire in 1 hour. If you did not request this, you can safely ignore this email.</p>
            </div>
            """;

        return new EmailMessage(recipientEmail, subject, plain, html);
    }

    public static EmailMessage PasswordChanged(string recipientEmail)
    {
        var subject = "Your password has been changed";
        var plain = "The password for your account was recently changed. All other active sessions have been revoked. If you did not make this change, please reset your password immediately.";
        var html = """
            <div style="font-family: sans-serif; line-height: 1.5; color: #333;">
              <h2>Password Changed</h2>
              <p>The password for your account was recently changed.</p>
              <p>All other active sessions have been revoked for your security.</p>
              <p style="color: #ef4444; font-weight: bold;">If you did not perform this change, contact support and reset your password immediately.</p>
            </div>
            """;

        return new EmailMessage(recipientEmail, subject, plain, html);
    }

    public static EmailMessage LockedOut(string recipientEmail)
    {
        var subject = "Security Alert: Account temporarily locked";
        var plain = "Your account has been temporarily locked due to 5 consecutive failed login attempts. It will automatically unlock in 15 minutes.";
        var html = """
            <div style="font-family: sans-serif; line-height: 1.5; color: #333;">
              <h2>Security Alert: Account Temporarily Locked</h2>
              <p>Your account was temporarily locked because of 5 consecutive failed sign-in attempts.</p>
              <p>Your account will unlock automatically in 15 minutes. No further action is required.</p>
              <p style="color: #666; font-size: 14px;">If you did not make these attempts, someone may be trying to access your account.</p>
            </div>
            """;

        return new EmailMessage(recipientEmail, subject, plain, html);
    }

    public static EmailMessage TwoFactorEnabled(string recipientEmail)
    {
        var subject = "Two-factor authentication enabled";
        var plain = "Two-factor authentication (TOTP) has been successfully activated on your account.";
        var html = """
            <div style="font-family: sans-serif; line-height: 1.5; color: #333;">
              <h2>Two-Factor Authentication Enabled</h2>
              <p>Two-factor authentication (authenticator app) is now active on your account.</p>
              <p>Future sign-ins will require your 6-digit one-time code.</p>
            </div>
            """;

        return new EmailMessage(recipientEmail, subject, plain, html);
    }

    public static EmailMessage TwoFactorDisabled(string recipientEmail)
    {
        var subject = "Security Alert: Two-factor authentication disabled";
        var plain = "Two-factor authentication was disabled on your account. If you did not authorize this, please re-enable it immediately.";
        var html = """
            <div style="font-family: sans-serif; line-height: 1.5; color: #333;">
              <h2>Security Alert: Two-Factor Authentication Disabled</h2>
              <p>Two-factor authentication was recently removed from your account.</p>
              <p style="color: #ef4444; font-weight: bold;">If you did not authorize this action, please secure your account immediately.</p>
            </div>
            """;

        return new EmailMessage(recipientEmail, subject, plain, html);
    }
}
