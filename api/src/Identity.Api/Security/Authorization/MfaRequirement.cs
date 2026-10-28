using Microsoft.AspNetCore.Authorization;

namespace Identity.Api.Security.Authorization;

/// <summary>
/// Authorization requirement verifying that two-factor authentication (MFA) was satisfied for this session.
/// </summary>
public sealed class MfaRequirement : IAuthorizationRequirement;

public sealed class MfaRequirementHandler : AuthorizationHandler<MfaRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MfaRequirement requirement)
    {
        // Check 'amr' (Authentication Method Reference) claims
        var amrClaims = context.User.FindAll("amr").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (amrClaims.Contains("otp") || amrClaims.Contains("mfa"))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
