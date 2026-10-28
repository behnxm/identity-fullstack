using Identity.Api.Common.Options;
using Identity.Api.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Api.Security.Password;

/// <summary>
/// NIST SP 800-63B compliant password validator.
/// Enforces length (12-128 chars), rejects common passwords, rejects personal identifiers,
/// and checks against known breaches via IBreachedPasswordChecker.
/// </summary>
public sealed class CustomPasswordValidator(
    IBreachedPasswordChecker breachedPasswordChecker,
    IOptions<SecurityPolicyOptions> options,
    ILogger<CustomPasswordValidator> logger)
    : IPasswordValidator<AppUser>
{
    private readonly SecurityPolicyOptions _options = options.Value;

    // Common passwords list (lowercase for case-insensitive matching)
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password12345",
        "123456789012",
        "qwertyuiop12",
        "admin1234567",
        "welcome123456",
        "letmein123456",
        "iloveyou12345",
        "monkey1234567",
        "dragon1234567",
        "football12345",
        "master1234567",
        "sunshine12345",
        "princess12345",
        "superman12345",
        "trustno112345",
        "charlie123456",
        "starwars12345",
        "computer12345",
        "jordan1234567",
        "harley1234567"
    };

    public async Task<IdentityResult> ValidateAsync(UserManager<AppUser> manager, AppUser user, string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordEmpty",
                Description = "Password cannot be empty."
            });
        }

        var errors = new List<IdentityError>();

        // 1. Length requirements: minimum 12, maximum 128 characters
        if (password.Length < 12)
        {
            errors.Add(new IdentityError
            {
                Code = "PasswordTooShort",
                Description = "Password must be at least 12 characters long."
            });
        }

        if (password.Length > 128)
        {
            errors.Add(new IdentityError
            {
                Code = "PasswordTooLong",
                Description = "Password must not exceed 128 characters."
            });
        }

        // 2. Reject common passwords blocklist
        if (CommonPasswords.Contains(password))
        {
            errors.Add(new IdentityError
            {
                Code = "PasswordTooCommon",
                Description = "This password is too common and easily guessed."
            });
        }

        // 3. Reject passwords containing personal identifiers (email local part, display name)
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            var emailLocalPart = user.Email.Split('@')[0];
            if (emailLocalPart.Length >= 3 && password.Contains(emailLocalPart, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new IdentityError
                {
                    Code = "PasswordContainsEmail",
                    Description = "Password cannot contain parts of your email address."
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            var nameParts = user.DisplayName.Split([' ', '.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in nameParts)
            {
                if (part.Length >= 3 && password.Contains(part, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(new IdentityError
                    {
                        Code = "PasswordContainsDisplayName",
                        Description = "Password cannot contain parts of your display name."
                    });
                    break;
                }
            }
        }

        // 4. Optional breached password check
        if (_options.BreachedPasswordCheckEnabled)
        {
            try
            {
                var isBreached = await breachedPasswordChecker.IsPasswordBreachedAsync(password);
                if (isBreached)
                {
                    errors.Add(new IdentityError
                    {
                        Code = "PasswordBreached",
                        Description = "This password has been exposed in a known data breach and is unsafe to use."
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to verify password with breached password checker; failing open.");
            }
        }

        return errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed(errors.ToArray());
    }
}
