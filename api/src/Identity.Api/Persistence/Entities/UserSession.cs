namespace Identity.Api.Persistence.Entities;

/// <summary>
/// Server-side session record for token binding, idle tracking, and instant revocation.
/// </summary>
public class UserSession
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevocationReason { get; set; }

    public string? UserAgent { get; set; }

    public string? IpAddress { get; set; }

    /// <summary>
    /// Set when the user satisfies MFA (TOTP or recovery code) during this session.
    /// Drives the 'amr' claim (["pwd", "otp"]) and MFA policy evaluation.
    /// </summary>
    public DateTimeOffset? MfaVerifiedAt { get; set; }

    public bool RememberMe { get; set; }

    public bool IsActive(DateTimeOffset utcNow) =>
        RevokedAt == null && ExpiresAt > utcNow;

    public AppUser User { get; set; } = null!;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
