namespace Identity.Api.Common.Options;

/// <summary>
/// Security policy options for NIST 800-63B password rules, lockout, and PBKDF2 iterations.
/// </summary>
public sealed class SecurityPolicyOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// PBKDF2 HMAC-SHA512 iterations. OWASP recommends at least 210,000 iterations for PBKDF2-HMAC-SHA512.
    /// </summary>
    public int Pbkdf2Iterations { get; set; } = 210_000;

    public int MaxFailedAccessAttempts { get; set; } = 5;
    public TimeSpan DefaultLockoutTimeSpan { get; set; } = TimeSpan.FromMinutes(15);
    public bool BreachedPasswordCheckEnabled { get; set; } = false;
    public string AuthenticatorIssuer { get; set; } = "IdentityFullstack";
}
