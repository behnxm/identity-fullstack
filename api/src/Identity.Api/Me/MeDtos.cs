using System.ComponentModel.DataAnnotations;

namespace Identity.Api.Me;

/// <summary>Request payload for updating user profile information.</summary>
public sealed record UpdateProfileRequest
{
    [Required, MinLength(2), MaxLength(100)]
    public required string DisplayName { get; init; }
}

/// <summary>Request payload for updating user password.</summary>
public sealed record ChangePasswordRequest
{
    [Required]
    public required string CurrentPassword { get; init; }

    [Required, MinLength(12), MaxLength(128)]
    public required string NewPassword { get; init; }
}

/// <summary>Setup payload for configuring two-factor authentication.</summary>
public sealed record TwoFactorSetupResponse(
    string SharedKey,
    string AuthenticatorUri);

/// <summary>Request payload for verifying and enabling two-factor authentication.</summary>
public sealed record EnableTwoFactorRequest
{
    [Required, StringLength(7, MinimumLength = 6)]
    public required string Code { get; init; }
}

/// <summary>Response containing newly generated one-time recovery codes and fresh tokens.</summary>
public sealed record EnableTwoFactorResponse(
    IReadOnlyList<string> RecoveryCodes,
    string AccessToken,
    int ExpiresIn,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAt);

/// <summary>Request payload for disabling two-factor authentication.</summary>
public sealed record DisableTwoFactorRequest
{
    [Required]
    public required string Password { get; init; }

    [Required, StringLength(7, MinimumLength = 6)]
    public required string Code { get; init; }
}

/// <summary>Response containing freshly regenerated one-time recovery codes.</summary>
public sealed record RecoveryCodesResponse(
    IReadOnlyList<string> RecoveryCodes);

/// <summary>Active session summary item.</summary>
public sealed record UserSessionResponse(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset ExpiresAt,
    string? IpAddress,
    string? UserAgent,
    bool IsCurrent);

/// <summary>Server-evaluated policy item with allowance state and explicit reason.</summary>
public sealed record PolicyEvaluationItem(
    bool Allowed,
    string? Reason);
