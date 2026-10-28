using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Identity.Api.Persistence;
using Identity.Api.Persistence.Entities;
using Microsoft.Extensions.Logging;

namespace Identity.Api.Auditing;

public interface ISecurityAuditor
{
    Task AuditAsync(
        string eventType,
        Guid? userId,
        string? ipAddress,
        string? userAgent,
        object? details = null,
        CancellationToken ct = default);

    Task AuditUnknownEmailAsync(
        string eventType,
        string rawEmail,
        string? ipAddress,
        string? userAgent,
        object? details = null,
        CancellationToken ct = default);
}

public sealed class SecurityAuditor(
    AppDbContext db,
    TimeProvider timeProvider,
    ILogger<SecurityAuditor> logger)
    : ISecurityAuditor
{
    public async Task AuditAsync(
        string eventType,
        Guid? userId,
        string? ipAddress,
        string? userAgent,
        object? details = null,
        CancellationToken ct = default)
    {
        var json = details != null ? JsonSerializer.Serialize(details) : null;
        var evt = new SecurityEvent
        {
            Id = Guid.CreateVersion7(),
            EventType = eventType,
            UserId = userId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = timeProvider.GetUtcNow(),
            DetailsJson = json
        };

        db.SecurityEvents.Add(evt);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Security audit event recorded: {EventType} for user {UserId}", eventType, userId);
    }

    public async Task AuditUnknownEmailAsync(
        string eventType,
        string rawEmail,
        string? ipAddress,
        string? userAgent,
        object? details = null,
        CancellationToken ct = default)
    {
        // Never log or persist unknown raw email addresses to prevent PII harvesting. Hash with SHA-256 instead.
        var emailHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawEmail.Trim().ToLowerInvariant())));

        var detailsDict = details != null
            ? JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(details)) ?? []
            : new Dictionary<string, object?>();

        detailsDict["emailHash"] = emailHash;

        await AuditAsync(eventType, null, ipAddress, userAgent, detailsDict, ct);
    }
}
