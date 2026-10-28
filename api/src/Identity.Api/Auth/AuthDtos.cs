using System.ComponentModel.DataAnnotations;

namespace Identity.Api.Auth;

/// <summary>Request payload for user registration.</summary>
public sealed record RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }

    [Required, MinLength(12), MaxLength(128)]
    public required string Password { get; init; }

    [Required, MinLength(2), MaxLength(100)]
    public required string DisplayName { get; init; }
}

/// <summary>Request payload for user login.</summary>
public sealed record LoginRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }

    [Required, MaxLength(128)]
    public required string Password { get; init; }

    public bool RememberMe { get; init; }
}

/// <summary>Request payload for completing two-factor login.</summary>
public sealed record TwoFactorLoginRequest
{
    [Required]
    public required string ChallengeToken { get; init; }

    public string? Code { get; init; }

    public string? RecoveryCode { get; init; }
}

/// <summary>Request payload for exchanging a rotating refresh token.</summary>
public sealed record RefreshTokenRequest
{
    [Required]
    public required string RefreshToken { get; init; }
}

/// <summary>Request payload for confirming email with token from fragment.</summary>
public sealed record ConfirmEmailRequest
{
    [Required]
    public required Guid UserId { get; init; }

    [Required]
    public required string Token { get; init; }
}

/// <summary>Request payload for resending email confirmation link.</summary>
public sealed record ResendConfirmationRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }
}

/// <summary>Request payload for initiating password reset.</summary>
public sealed record ForgotPasswordRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public required string Email { get; init; }
}

/// <summary>Request payload for resetting password using verification token.</summary>
public sealed record ResetPasswordRequest
{
    [Required]
    public required Guid UserId { get; init; }

    [Required]
    public required string Token { get; init; }

    [Required, MinLength(12), MaxLength(128)]
    public required string NewPassword { get; init; }
}

/// <summary>User summary included in authentication responses.</summary>
public sealed record UserSummaryResponse(
    Guid Id,
    string Email,
    string DisplayName,
    bool EmailConfirmed,
    bool TwoFactorEnabled,
    IReadOnlyList<string> Roles);

/// <summary>Authentication result response returned to clients/BFF.</summary>
public sealed record AuthResponse(
    string Status,
    string? AccessToken,
    int? ExpiresIn,
    string? RefreshToken,
    DateTimeOffset? RefreshExpiresAt,
    Guid? SessionId,
    string? ChallengeToken,
    UserSummaryResponse? User);
