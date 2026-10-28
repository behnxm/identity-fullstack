using System.Net;
using System.Security.Claims;
using Identity.Api.Auditing;
using Identity.Api.Auth;
using Identity.Api.Common.Constants;
using Identity.Api.Common.Options;
using Identity.Api.Common.Results;
using Identity.Api.Email;
using Identity.Api.Persistence.Entities;
using Identity.Api.Security.Authorization;
using Identity.Api.Security.Sessions;
using Identity.Api.Security.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Api.Me;

/// <summary>
/// Exposes user profile, session, two-factor enrollment, and policy evaluation endpoints under /api/v1/me.
/// </summary>
public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/me")
            .WithTags("Me")
            .RequireAuthorization();

        group.MapGet("/", GetMeAsync)
            .WithName("GetMe")
            .WithSummary("Get profile of authenticated user")
            .Produces<UserSummaryResponse>(StatusCodes.Status200OK);

        group.MapGet("/policies", GetPoliciesAsync)
            .WithName("GetPolicies")
            .WithSummary("Evaluate all authorization policies for current caller")
            .Produces<Dictionary<string, PolicyEvaluationItem>>(StatusCodes.Status200OK);

        group.MapPut("/profile", UpdateProfileAsync)
            .RequireAuthorization(Policies.PanelUser)
            .WithName("UpdateProfile")
            .WithSummary("Update user display name")
            .Produces<UserSummaryResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapGet("/sessions", GetSessionsAsync)
            .WithName("GetSessions")
            .WithSummary("List active user sessions")
            .Produces<IReadOnlyList<UserSessionResponse>>(StatusCodes.Status200OK);

        group.MapDelete("/sessions/{id:guid}", RevokeSessionAsync)
            .WithName("RevokeSession")
            .WithSummary("Revoke a specific active session")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/security/password", ChangePasswordAsync)
            .WithName("ChangePassword")
            .WithSummary("Update password and revoke all other sessions")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapGet("/security/2fa/setup", GetTwoFactorSetupAsync)
            .WithName("GetTwoFactorSetup")
            .WithSummary("Generate shared TOTP key and otpauth URI")
            .Produces<TwoFactorSetupResponse>(StatusCodes.Status200OK);

        group.MapPost("/security/2fa/enable", EnableTwoFactorAsync)
            .WithName("EnableTwoFactor")
            .WithSummary("Verify TOTP code, enable 2FA, issue recovery codes, and step-up token")
            .Produces<EnableTwoFactorResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapPost("/security/2fa/disable", DisableTwoFactorAsync)
            .WithName("DisableTwoFactor")
            .WithSummary("Disable 2FA (forbidden for accounts holding admin permissions)")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/security/2fa/recovery-codes", RegenerateRecoveryCodesAsync)
            .WithName("RegenerateRecoveryCodes")
            .WithSummary("Generate new single-use recovery codes")
            .Produces<RecoveryCodesResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    private static async Task<IResult> GetMeAsync(
        ClaimsPrincipal caller,
        UserManager<AppUser> userManager)
    {
        var sub = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return TypedResults.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);

        return TypedResults.Ok(new UserSummaryResponse(
            user.Id,
            user.Email!,
            user.DisplayName,
            user.EmailConfirmed,
            user.TwoFactorEnabled,
            roles.ToList()));
    }

    private static async Task<IResult> GetPoliciesAsync(
        ClaimsPrincipal caller,
        IAuthorizationService authService,
        UserManager<AppUser> userManager)
    {
        var result = new Dictionary<string, PolicyEvaluationItem>(StringComparer.OrdinalIgnoreCase);

        var sub = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.FindFirstValue("sub");
        var user = Guid.TryParse(sub, out var userId) ? await userManager.FindByIdAsync(userId.ToString()) : null;

        var emailVerified = user?.EmailConfirmed ?? false;
        var hasMfa = caller.FindAll("amr").Any(c => string.Equals(c.Value, "otp", StringComparison.OrdinalIgnoreCase)
                                                || string.Equals(c.Value, "mfa", StringComparison.OrdinalIgnoreCase));

        foreach (var policy in Policies.EvaluatablePolicies)
        {
            var authResult = await authService.AuthorizeAsync(caller, policy);
            if (authResult.Succeeded)
            {
                result[policy] = new PolicyEvaluationItem(Allowed: true, Reason: null);
            }
            else
            {
                // Map the failed requirement to a clean, UI-friendly reason code
                string reason;
                if (!emailVerified && policy == Policies.PanelUser)
                {
                    reason = "email_not_confirmed";
                }
                else if (!hasMfa && (policy == Policies.PanelAdmin ||
                                     policy == Policies.UsersRead ||
                                     policy == Policies.AuditRead ||
                                     policy == Policies.UsersManage ||
                                     policy == Policies.NotesModerate))
                {
                    reason = "mfa_required";
                }
                else
                {
                    reason = "missing_permission";
                }

                result[policy] = new PolicyEvaluationItem(Allowed: false, Reason: reason);
            }
        }

        return TypedResults.Ok(result);
    }

    private static async Task<IResult> UpdateProfileAsync(
        [FromBody] UpdateProfileRequest request,
        ClaimsPrincipal caller,
        UserManager<AppUser> userManager)
    {
        var sub = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return TypedResults.Unauthorized();
        }

        user.DisplayName = request.DisplayName;
        await userManager.UpdateAsync(user);

        var roles = await userManager.GetRolesAsync(user);

        return TypedResults.Ok(new UserSummaryResponse(
            user.Id,
            user.Email!,
            user.DisplayName,
            user.EmailConfirmed,
            user.TwoFactorEnabled,
            roles.ToList()));
    }

    private static async Task<IResult> GetSessionsAsync(
        ClaimsPrincipal caller,
        ISessionService sessionService)
    {
        var sub = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.FindFirstValue("sub");
        var sid = caller.FindFirstValue("sid");

        if (!Guid.TryParse(sub, out var userId) || !Guid.TryParse(sid, out var currentSessionId))
        {
            return TypedResults.Unauthorized();
        }

        var sessions = await sessionService.GetUserSessionsAsync(userId, default);

        var response = sessions.Select(s => new UserSessionResponse(
            s.Id,
            s.CreatedAt,
            s.LastSeenAt,
            s.ExpiresAt,
            s.IpAddress,
            s.UserAgent,
            IsCurrent: s.Id == currentSessionId)).ToList();

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> RevokeSessionAsync(
        Guid id,
        ClaimsPrincipal caller,
        ISessionService sessionService,
        ISecurityAuditor auditor,
        HttpContext context,
        CancellationToken ct)
    {
        var sub = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        var sessions = await sessionService.GetUserSessionsAsync(userId, ct);
        if (!sessions.Any(s => s.Id == id))
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status404NotFound,
                "session.not_found",
                "Session not found",
                "The requested session does not exist or has already been revoked.",
                context.Request.Path);
        }

        await sessionService.RevokeSessionAsync(id, "user_revoked", ct);
        await auditor.AuditAsync(
            SecurityEventTypes.SessionRevoked,
            userId,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            new { sessionId = id },
            ct);

        return TypedResults.Ok(new { status = "session_revoked" });
    }

    private static async Task<IResult> ChangePasswordAsync(
        [FromBody] ChangePasswordRequest request,
        ClaimsPrincipal caller,
        UserManager<AppUser> userManager,
        ISessionService sessionService,
        IEmailQueue emailQueue,
        ISecurityAuditor auditor,
        HttpContext context,
        CancellationToken ct)
    {
        var sub = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.FindFirstValue("sub");
        var sid = caller.FindFirstValue("sid");

        if (!Guid.TryParse(sub, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        var currentSessionId = Guid.TryParse(sid, out var s) ? s : (Guid?)null;

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return TypedResults.Unauthorized();
        }

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status400BadRequest,
                AuthErrorCodes.PasswordMismatch,
                "Password change failed",
                result.Errors.First().Description,
                context.Request.Path);
        }

        // Revoke all OTHER sessions immediately
        await sessionService.RevokeAllUserSessionsAsync(userId, "password_changed", currentSessionId, ct);

        // Send email alert
        var notice = EmailTemplates.PasswordChanged(user.Email!);
        await emailQueue.QueueEmailAsync(notice, ct);

        await auditor.AuditAsync(
            SecurityEventTypes.PasswordChanged,
            user.Id,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            null,
            ct);

        return TypedResults.Ok(new { status = "password_changed" });
    }

    private static async Task<IResult> GetTwoFactorSetupAsync(
        ClaimsPrincipal caller,
        UserManager<AppUser> userManager,
        IOptions<SecurityPolicyOptions> options)
    {
        var sub = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return TypedResults.Unauthorized();
        }

        var unformattedKey = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await userManager.GetAuthenticatorKeyAsync(user);
        }

        var issuer = WebUtility.UrlEncode(options.Value.AuthenticatorIssuer);
        var email = WebUtility.UrlEncode(user.Email!);
        var authenticatorUri = $"otpauth://totp/{issuer}:{email}?secret={unformattedKey}&issuer={issuer}&digits=6";

        return TypedResults.Ok(new TwoFactorSetupResponse(unformattedKey!, authenticatorUri));
    }

    private static async Task<IResult> EnableTwoFactorAsync(
        [FromBody] EnableTwoFactorRequest request,
        ClaimsPrincipal caller,
        UserManager<AppUser> userManager,
        RoleManager<AppRole> roleManager,
        ISessionService sessionService,
        ITokenService tokenService,
        ISecurityAuditor auditor,
        IEmailQueue emailQueue,
        TimeProvider timeProvider,
        IOptions<JwtOptions> jwtOptions,
        HttpContext context,
        CancellationToken ct)
    {
        var sub = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.FindFirstValue("sub");
        var sid = caller.FindFirstValue("sid");

        if (!Guid.TryParse(sub, out var userId) || !Guid.TryParse(sid, out var sessionId))
        {
            return TypedResults.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return TypedResults.Unauthorized();
        }

        if (user.TwoFactorEnabled)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status400BadRequest,
                AuthErrorCodes.InvalidTotp,
                "Two-factor already enabled",
                "Two-factor authentication is already active on this account.",
                context.Request.Path);
        }

        var cleanCode = request.Code.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
        var now = timeProvider.GetUtcNow();
        var currentStep = now.ToUnixTimeSeconds() / 30;

        if (user.LastTotpStep.HasValue && user.LastTotpStep.Value == currentStep)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status400BadRequest,
                AuthErrorCodes.InvalidTotp,
                "Code already used",
                "This one-time code has already been used in this time window.",
                context.Request.Path);
        }

        var is2faTokenValid = await userManager.VerifyTwoFactorTokenAsync(
            user, userManager.Options.Tokens.AuthenticatorTokenProvider, cleanCode);

        if (!is2faTokenValid)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status400BadRequest,
                AuthErrorCodes.InvalidTotp,
                "Verification failed",
                "The 6-digit verification code was invalid.",
                context.Request.Path);
        }

        user.LastTotpStep = currentStep;
        await userManager.UpdateAsync(user);
        await userManager.SetTwoFactorEnabledAsync(user, true);

        // Generate 10 single-use recovery codes
        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

        // Mark the current session as MFA-verified
        await sessionService.MarkMfaVerifiedAsync(sessionId, ct);

        // Rotate and return a fresh token pair with AMR = ["pwd", "otp"]
        var session = (await sessionService.GetUserSessionsAsync(userId, ct)).First(s => s.Id == sessionId);

        var roles = await userManager.GetRolesAsync(user);
        var permissions = await GetPermissionsForRolesAsync(roleManager, roles);

        var accessToken = tokenService.GenerateAccessToken(user, session, roles.ToList(), permissions, now);
        var (refreshedSession, newRawRefreshToken) = await sessionService.CreateSessionAsync(
            user.Id, session.RememberMe, context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(), mfaVerified: true, ct);

        // Send email alert
        var notice = EmailTemplates.TwoFactorEnabled(user.Email!);
        await emailQueue.QueueEmailAsync(notice, ct);

        await auditor.AuditAsync(
            SecurityEventTypes.TwoFactorEnabled,
            user.Id,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            null,
            ct);

        return TypedResults.Ok(new EnableTwoFactorResponse(
            recoveryCodes!.ToList(),
            accessToken,
            (int)jwtOptions.Value.AccessTokenLifetime.TotalSeconds,
            newRawRefreshToken,
            refreshedSession.ExpiresAt));
    }

    private static async Task<IResult> DisableTwoFactorAsync(
        [FromBody] DisableTwoFactorRequest request,
        ClaimsPrincipal caller,
        UserManager<AppUser> userManager,
        RoleManager<AppRole> roleManager,
        ISecurityAuditor auditor,
        IEmailQueue emailQueue,
        HttpContext context,
        CancellationToken ct)
    {
        var sub = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return TypedResults.Unauthorized();
        }

        // Rule: Users holding admin-panel permissions cannot disable two-factor!
        var roles = await userManager.GetRolesAsync(user);
        var permissions = await GetPermissionsForRolesAsync(roleManager, roles);

        var adminPermissions = new[] { Permissions.UsersRead, Permissions.AuditRead, Permissions.UsersManage, Permissions.NotesModerate };
        if (permissions.Any(p => adminPermissions.Contains(p)))
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status409Conflict,
                AuthErrorCodes.MfaMandatory,
                "MFA is mandatory",
                "Users holding administrative or support permissions are required to maintain active two-factor authentication.",
                context.Request.Path);
        }

        // Verify password
        var passwordValid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status400BadRequest,
                AuthErrorCodes.PasswordMismatch,
                "Password verification failed",
                "The provided password was incorrect.",
                context.Request.Path);
        }

        // Verify TOTP code
        var cleanCode = request.Code.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
        var codeValid = await userManager.VerifyTwoFactorTokenAsync(
            user, userManager.Options.Tokens.AuthenticatorTokenProvider, cleanCode);

        if (!codeValid)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status400BadRequest,
                AuthErrorCodes.InvalidTotp,
                "Code verification failed",
                "The provided one-time code was invalid.",
                context.Request.Path);
        }

        await userManager.SetTwoFactorEnabledAsync(user, false);

        var notice = EmailTemplates.TwoFactorDisabled(user.Email!);
        await emailQueue.QueueEmailAsync(notice, ct);

        await auditor.AuditAsync(
            SecurityEventTypes.TwoFactorDisabled,
            user.Id,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            null,
            ct);

        return TypedResults.Ok(new { status = "two_factor_disabled" });
    }

    private static async Task<IResult> RegenerateRecoveryCodesAsync(
        ClaimsPrincipal caller,
        UserManager<AppUser> userManager,
        HttpContext context)
    {
        var sub = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null || !user.TwoFactorEnabled)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status400BadRequest,
                AuthErrorCodes.InvalidToken,
                "Two-factor authentication not enabled",
                "You must enable two-factor authentication before generating recovery codes.",
                context.Request.Path);
        }

        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        return TypedResults.Ok(new RecoveryCodesResponse(recoveryCodes!.ToList()));
    }

    private static async Task<IReadOnlyList<string>> GetPermissionsForRolesAsync(
        RoleManager<AppRole> roleManager,
        IEnumerable<string> roleNames)
    {
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var roleName in roleNames)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role != null)
            {
                var claims = await roleManager.GetClaimsAsync(role);
                foreach (var claim in claims.Where(c => c.Type == "permission"))
                {
                    permissions.Add(claim.Value);
                }
            }
        }
        return permissions.ToList();
    }
}
