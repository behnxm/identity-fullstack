namespace Identity.Api.Persistence.Entities;

/// <summary>
/// Opaque refresh token metadata. Raw tokens are never stored; only their SHA-256 hashes are persisted.
/// Supports rotation on every exchange, replacement tracking, and reuse detection.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid SessionId { get; set; }

    /// <summary>
    /// Hex-encoded SHA-256 hash of the 256-bit random opaque refresh token.
    /// </summary>
    public required string TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

    public string? ReplacedByTokenHash { get; set; }

    public bool IsUsed => UsedAt.HasValue;

    public bool IsExpired(DateTimeOffset utcNow) => ExpiresAt <= utcNow;

    public UserSession Session { get; set; } = null!;
}
