using System.Security.Cryptography;
using System.Text;
using Identity.Api.Common.Constants;
using Identity.Api.Common.Options;
using Identity.Api.Persistence;
using Identity.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Api.Security.Sessions;

/// <summary>
/// Implements session lifecycle, opaque refresh token rotation, grace-window race detection, and instant token revocation.
/// </summary>
public sealed class SessionService(
    AppDbContext db,
    HybridCache cache,
    TimeProvider timeProvider,
    IOptions<UserSessionOptions> sessionOptions,
    ILogger<SessionService> logger)
    : ISessionService
{
    private readonly UserSessionOptions _options = sessionOptions.Value;

    public async Task<(UserSession Session, string RawRefreshToken)> CreateSessionAsync(
        Guid userId,
        bool rememberMe,
        string? ipAddress,
        string? userAgent,
        bool mfaVerified,
        CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();

        // Enforce maximum active sessions limit per user (revoke oldest)
        var activeSessions = await db.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);

        if (activeSessions.Count >= _options.MaxActiveSessionsPerUser)
        {
            var sessionsToRevokeCount = activeSessions.Count - _options.MaxActiveSessionsPerUser + 1;
            for (var i = 0; i < sessionsToRevokeCount; i++)
            {
                var oldest = activeSessions[i];
                oldest.RevokedAt = now;
                oldest.RevocationReason = "session_cap_exceeded";
                await EvictSessionCacheAsync(oldest.Id, ct);
                logger.LogInformation("Revoked oldest session {SessionId} for user {UserId} due to session cap", oldest.Id, userId);
            }
        }

        var absoluteLifetime = rememberMe ? _options.RememberMeAbsoluteLifetime : _options.DefaultAbsoluteLifetime;
        var idleLifetime = rememberMe ? _options.RememberMeIdleLifetime : _options.DefaultIdleLifetime;
        var initialExpiresAt = now.Add(idleLifetime < absoluteLifetime ? idleLifetime : absoluteLifetime);

        var session = new UserSession
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = initialExpiresAt,
            RememberMe = rememberMe,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            MfaVerifiedAt = mfaVerified ? now : null
        };

        var rawRefreshToken = GenerateOpaqueToken();
        var tokenHash = HashToken(rawRefreshToken);

        var refreshToken = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            SessionId = session.Id,
            TokenHash = tokenHash,
            CreatedAt = now,
            ExpiresAt = session.ExpiresAt
        };

        db.UserSessions.Add(session);
        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync(ct);

        return (session, rawRefreshToken);
    }

    public async Task<RefreshResult> RefreshSessionAsync(
        string rawRefreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var incomingHash = HashToken(rawRefreshToken);

        // Find token by exact hash
        var token = await db.RefreshTokens
            .Include(r => r.Session)
            .ThenInclude(s => s.User)
            .FirstOrDefaultAsync(r => r.TokenHash == incomingHash, ct);

        if (token == null)
        {
            logger.LogWarning("Refresh failed: Token hash not found");
            return RefreshResult.Invalid();
        }

        // Constant-time hash verification against stored hash
        var storedHashBytes = Encoding.UTF8.GetBytes(token.TokenHash);
        var incomingHashBytes = Encoding.UTF8.GetBytes(incomingHash);
        if (!CryptographicOperations.FixedTimeEquals(storedHashBytes, incomingHashBytes))
        {
            return RefreshResult.Invalid();
        }

        var session = token.Session;

        // Check if token was already used (reuse detection)
        if (token.IsUsed)
        {
            // Benign client race window check
            if (token.UsedAt.HasValue && (now - token.UsedAt.Value) <= _options.RefreshGraceWindow)
            {
                logger.LogInformation("Refresh token presented within grace window ({Elapsed}s <= {Window}s) for session {SessionId}. Returning refresh_race.",
                    (now - token.UsedAt.Value).TotalSeconds, _options.RefreshGraceWindow.TotalSeconds, session.Id);
                return RefreshResult.RaceDetected();
            }

            // Outside grace window: Malicious token reuse detected! Revoke the whole session immediately.
            session.RevokedAt = now;
            session.RevocationReason = "refresh_reuse_detected";
            await db.SaveChangesAsync(ct);
            await EvictSessionCacheAsync(session.Id, ct);

            logger.LogWarning("SECURITY ALERT: Refresh token reuse detected for session {SessionId}, user {UserId}. Session revoked.",
                session.Id, session.UserId);

            return RefreshResult.ReuseDetected(session.Id, session.UserId);
        }

        // Verify session and token validity
        if (!session.IsActive(now) || token.IsExpired(now))
        {
            return RefreshResult.Expired();
        }

        // Check user lockout or deletion
        if (session.User.LockoutEnd.HasValue && session.User.LockoutEnd.Value > now)
        {
            logger.LogWarning("Refresh rejected: User {UserId} is locked out", session.UserId);
            return RefreshResult.Invalid();
        }

        // Rotate token
        var newRawRefreshToken = GenerateOpaqueToken();
        var newHash = HashToken(newRawRefreshToken);

        token.UsedAt = now;
        token.ReplacedByTokenHash = newHash;

        // Slide idle expiration (bounded by absolute lifetime)
        var absoluteLifetime = session.RememberMe ? _options.RememberMeAbsoluteLifetime : _options.DefaultAbsoluteLifetime;
        var idleLifetime = session.RememberMe ? _options.RememberMeIdleLifetime : _options.DefaultIdleLifetime;
        var maxAbsolute = session.CreatedAt.Add(absoluteLifetime);
        var newIdle = now.Add(idleLifetime);

        session.ExpiresAt = newIdle < maxAbsolute ? newIdle : maxAbsolute;
        session.LastSeenAt = now;
        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            session.IpAddress = ipAddress;
        }
        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            session.UserAgent = userAgent;
        }

        var newRefreshToken = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            SessionId = session.Id,
            TokenHash = newHash,
            CreatedAt = now,
            ExpiresAt = session.ExpiresAt
        };

        db.RefreshTokens.Add(newRefreshToken);
        await db.SaveChangesAsync(ct);

        await EvictSessionCacheAsync(session.Id, ct);

        return RefreshResult.Success(session, newRawRefreshToken);
    }

    public async Task RevokeSessionAsync(Guid sessionId, string reason, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var session = await db.UserSessions.FindAsync([sessionId], ct);
        if (session != null && session.RevokedAt == null)
        {
            session.RevokedAt = now;
            session.RevocationReason = reason;
            await db.SaveChangesAsync(ct);
            await EvictSessionCacheAsync(sessionId, ct);
            logger.LogInformation("Revoked session {SessionId}: {Reason}", sessionId, reason);
        }
    }

    public async Task RevokeAllUserSessionsAsync(Guid userId, string reason, Guid? exceptSessionId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var sessions = await db.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var s in sessions)
        {
            if (exceptSessionId.HasValue && s.Id == exceptSessionId.Value)
            {
                continue;
            }

            s.RevokedAt = now;
            s.RevocationReason = reason;
            await EvictSessionCacheAsync(s.Id, ct);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Revoked all sessions for user {UserId} (except {ExceptSessionId}): {Reason}",
            userId, exceptSessionId, reason);
    }

    public async Task<SessionValidationResult> ValidateSessionForTokenAsync(Guid sessionId, CancellationToken ct)
    {
        var cacheKey = $"session:{sessionId}";
        var cached = await cache.GetOrCreateAsync(
            cacheKey,
            async token =>
            {
                var now = timeProvider.GetUtcNow();
                var s = await db.UserSessions
                    .AsNoTracking()
                    .Include(x => x.User)
                    .FirstOrDefaultAsync(x => x.Id == sessionId, token);

                if (s == null)
                {
                    return new CachedSessionState(false, AuthErrorCodes.Unauthenticated, Guid.Empty, false);
                }

                if (s.RevokedAt != null)
                {
                    return new CachedSessionState(false, AuthErrorCodes.SessionRevoked, s.UserId, false);
                }

                if (s.ExpiresAt <= now)
                {
                    return new CachedSessionState(false, AuthErrorCodes.SessionExpired, s.UserId, false);
                }

                if (s.User.LockoutEnd.HasValue && s.User.LockoutEnd.Value > now)
                {
                    return new CachedSessionState(false, AuthErrorCodes.InvalidCredentials, s.UserId, false);
                }

                return new CachedSessionState(true, null, s.UserId, s.MfaVerifiedAt.HasValue);
            },
            new HybridCacheEntryOptions
            {
                Expiration = _options.HybridCacheTtl,
                LocalCacheExpiration = _options.HybridCacheTtl
            },
            cancellationToken: ct);

        return cached.IsValid
            ? SessionValidationResult.Valid(cached.UserId, cached.IsMfaVerified)
            : SessionValidationResult.Invalid(cached.ErrorCode ?? AuthErrorCodes.Unauthenticated);
    }

    public async Task<IReadOnlyList<UserSession>> GetUserSessionsAsync(Guid userId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        return await db.UserSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now)
            .OrderByDescending(s => s.LastSeenAt)
            .ToListAsync(ct);
    }

    public async Task MarkMfaVerifiedAsync(Guid sessionId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var session = await db.UserSessions.FindAsync([sessionId], ct);
        if (session != null)
        {
            session.MfaVerifiedAt = now;
            await db.SaveChangesAsync(ct);
            await EvictSessionCacheAsync(sessionId, ct);
        }
    }

    private async Task EvictSessionCacheAsync(Guid sessionId, CancellationToken ct)
    {
        try
        {
            await cache.RemoveAsync($"session:{sessionId}", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to evict hybrid cache for session {SessionId}", sessionId);
        }
    }

    public static string GenerateOpaqueToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32); // 256 bits of cryptographically strong randomness
        return Base64UrlEncoder.Encode(bytes);
    }

    public static string HashToken(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexStringLower(hash);
    }

    private record CachedSessionState(bool IsValid, string? ErrorCode, Guid UserId, bool IsMfaVerified);
}
