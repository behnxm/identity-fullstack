namespace Identity.Api.Auditing;

public static class SecurityEventTypes
{
    public const string LoginSucceeded = "LoginSucceeded";
    public const string LoginFailed = "LoginFailed";
    public const string LockedOut = "LockedOut";
    public const string RegisterRequested = "RegisterRequested";
    public const string EmailConfirmed = "EmailConfirmed";
    public const string PasswordResetRequested = "PasswordResetRequested";
    public const string PasswordReset = "PasswordReset";
    public const string PasswordChanged = "PasswordChanged";
    public const string TwoFactorEnabled = "TwoFactorEnabled";
    public const string TwoFactorDisabled = "TwoFactorDisabled";
    public const string RecoveryCodeUsed = "RecoveryCodeUsed";
    public const string SessionRevoked = "SessionRevoked";
    public const string RefreshReuseDetected = "RefreshReuseDetected";
    public const string RoleChanged = "RoleChanged";
    public const string UserLocked = "UserLocked";
    public const string UserUnlocked = "UserUnlocked";
}
