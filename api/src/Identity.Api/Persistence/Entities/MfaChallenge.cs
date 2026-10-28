namespace Identity.Api.Persistence.Entities;

/// <summary>
/// Short-lived, single-use two-factor login challenge token.
/// Stores only the SHA-256 hash of the challenge token with strict attempt limits and expiration.
/// </summary>
public class MfaChallenge
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string TokenHash { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; set; }

    public int AttemptsCount { get; set; }

    public bool IsConsumed { get; set; }

    public bool RememberMe { get; set; }

    public AppUser User { get; set; } = null!;
}
