namespace Identity.Api.Common.Constants;

/// <summary>
/// Stable error codes for RFC 9457 Problem Details responses.
/// </summary>
public static class AuthErrorCodes
{
    public const string Unauthenticated = "auth.unauthenticated";
    public const string InvalidCredentials = "auth.invalid_credentials";
    public const string SessionRevoked = "auth.session_revoked";
    public const string SessionExpired = "auth.session_expired";
    public const string TokenExpired = "auth.token_expired";
    public const string RefreshReuse = "auth.refresh_reuse";
    public const string RefreshRace = "auth.refresh_race";
    public const string EmailNotConfirmed = "auth.email_not_confirmed";
    public const string MfaRequired = "auth.mfa_required";
    public const string Forbidden = "auth.forbidden";
    public const string RateLimited = "auth.rate_limited";
    public const string LastAdmin = "auth.last_admin";
    public const string SelfModificationForbidden = "auth.self_modification_forbidden";
    public const string InvalidTotp = "auth.invalid_totp";
    public const string InvalidRecoveryCode = "auth.invalid_recovery_code";
    public const string ChallengeExpired = "auth.challenge_expired";
    public const string ChallengeExceeded = "auth.challenge_exceeded";
    public const string MfaMandatory = "auth.mfa_mandatory";
    public const string UserNotFound = "auth.user_not_found";
    public const string PasswordMismatch = "auth.password_mismatch";
    public const string InvalidToken = "auth.invalid_token";
}
