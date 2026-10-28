using Microsoft.AspNetCore.Identity;

namespace Identity.Api.Persistence.Entities;

/// <summary>
/// Custom identity user entity with Guid primary keys, display name, audit timestamps, and TOTP step tracking.
/// </summary>
public class AppUser : IdentityUser<Guid>
{
    public required string DisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>
    /// The last used TOTP time step index (Unix time / 30).
    /// Used for strict replay protection so an OTP code cannot be reused within the same time window.
    /// </summary>
    public long? LastTotpStep { get; set; }

    public ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();

    public ICollection<Note> Notes { get; set; } = new List<Note>();
}
