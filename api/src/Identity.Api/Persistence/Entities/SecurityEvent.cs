namespace Identity.Api.Persistence.Entities;

/// <summary>
/// Append-only security audit log entry.
/// Sensitive credentials, passwords, recovery codes, and tokens are NEVER stored in this table.
/// </summary>
public class SecurityEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string EventType { get; set; }

    public Guid? UserId { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public string? DetailsJson { get; set; }

    public AppUser? User { get; set; }
}
