using Identity.Api.Persistence.Entities;

namespace Identity.Api.Security.Sessions;

public record RefreshResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorCode { get; init; }
    public UserSession? Session { get; init; }
    public string? NewRawRefreshToken { get; init; }
    public Guid? RevokedSessionId { get; init; }
    public Guid? UserId { get; init; }

    public static RefreshResult Success(UserSession session, string newRawRefreshToken) =>
        new() { IsSuccess = true, Session = session, NewRawRefreshToken = newRawRefreshToken };

    public static RefreshResult ReuseDetected(Guid sessionId, Guid userId) =>
        new() { IsSuccess = false, ErrorCode = Common.Constants.AuthErrorCodes.RefreshReuse, RevokedSessionId = sessionId, UserId = userId };

    public static RefreshResult RaceDetected() =>
        new() { IsSuccess = false, ErrorCode = Common.Constants.AuthErrorCodes.RefreshRace };

    public static RefreshResult Expired() =>
        new() { IsSuccess = false, ErrorCode = Common.Constants.AuthErrorCodes.SessionExpired };

    public static RefreshResult Invalid() =>
        new() { IsSuccess = false, ErrorCode = Common.Constants.AuthErrorCodes.InvalidCredentials };
}

public record SessionValidationResult(bool IsValid, string? ErrorCode, Guid UserId, bool IsMfaVerified)
{
    public static SessionValidationResult Valid(Guid userId, bool isMfaVerified) =>
        new(true, null, userId, isMfaVerified);

    public static SessionValidationResult Invalid(string errorCode) =>
        new(false, errorCode, Guid.Empty, false);
}

/// <summary>
/// Service managing server-side user sessions, rotating refresh tokens, reuse detection, and session caching.
/// </summary>
public interface ISessionService
{
    Task<(UserSession Session, string RawRefreshToken)> CreateSessionAsync(
        Guid userId,
        bool rememberMe,
        string? ipAddress,
        string? userAgent,
        bool mfaVerified,
        CancellationToken ct);

    Task<RefreshResult> RefreshSessionAsync(
        string rawRefreshToken,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct);

    Task RevokeSessionAsync(Guid sessionId, string reason, CancellationToken ct);

    Task RevokeAllUserSessionsAsync(Guid userId, string reason, Guid? exceptSessionId, CancellationToken ct);

    Task<SessionValidationResult> ValidateSessionForTokenAsync(Guid sessionId, CancellationToken ct);

    Task<IReadOnlyList<UserSession>> GetUserSessionsAsync(Guid userId, CancellationToken ct);

    Task MarkMfaVerifiedAsync(Guid sessionId, CancellationToken ct);
}
