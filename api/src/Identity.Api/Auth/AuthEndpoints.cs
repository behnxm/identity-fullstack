using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Identity.Api.Auditing;
using Identity.Api.Common.Constants;
using Identity.Api.Common.Options;
using Identity.Api.Common.Results;
using Identity.Api.Email;
using Identity.Api.Middleware;
using Identity.Api.Persistence;
using Identity.Api.Persistence.Entities;
using Identity.Api.Security.Keys;
using Identity.Api.Security.Sessions;
using Identity.Api.Security.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Api.Auth;

/// <summary>
/// Exposes authentication, registration, recovery, and token management endpoints under /api/v1/auth.
/// </summary>
public static class AuthEndpoints
{
    // Pre-computed dummy hash to run on non-existent users for constant-time password verification
    private static readonly string DummyHash =
        new PasswordHasher<AppUser>().HashPassword(new AppUser { DisplayName = "Dummy" }, "DummyPassword!12345678");

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth")
            .WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimiterConfig.AuthSensitivePolicy)
            .WithName("Register")
            .WithSummary("Register a new account (anti-enumeration)")
            .Produces(StatusCodes.Status202Accepted)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapPost("/confirm-email", ConfirmEmailAsync)
            .AllowAnonymous()
            .WithName("ConfirmEmail")
            .WithSummary("Confirm email address using token from URL fragment")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapPost("/resend-confirmation", ResendConfirmationAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimiterConfig.AuthSensitivePolicy)
            .WithName("ResendConfirmation")
            .WithSummary("Resend email confirmation link (anti-enumeration)")
            .Produces(StatusCodes.Status202Accepted);

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimiterConfig.AuthLoginPolicy)
            .WithName("Login")
            .WithSummary("Authenticate with email and password")
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden);

        group.MapPost("/login/2fa", TwoFactorLoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimiterConfig.AuthLoginPolicy)
            .WithName("TwoFactorLogin")
            .WithSummary("Complete sign in with TOTP or recovery code")
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimiterConfig.AuthRefreshPolicy)
            .WithName("RefreshToken")
            .WithSummary("Exchange rotating refresh token for a fresh token pair")
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        group.MapPost("/forgot-password", ForgotPasswordAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimiterConfig.AuthSensitivePolicy)
            .WithName("ForgotPassword")
            .WithSummary("Initiate password reset (anti-enumeration)")
            .Produces(StatusCodes.Status202Accepted);

        group.MapPost("/reset-password", ResetPasswordAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimiterConfig.AuthSensitivePolicy)
            .WithName("ResetPassword")
            .WithSummary("Complete password reset with verification token")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .WithName("Logout")
            .WithSummary("Revoke caller's current session")
            .Produces(StatusCodes.Status200OK);

        group.MapPost("/logout-all", LogoutAllAsync)
            .RequireAuthorization()
            .WithName("LogoutAll")
            .WithSummary("Revoke all sessions for current user")
            .Produces(StatusCodes.Status200OK);

        // Discovery and Health
        endpoints.MapGet("/.well-known/jwks.json", GetJwks)
            .AllowAnonymous()
            .WithName("GetJwks")
            .WithSummary("Public JSON Web Key Set for verifying ES256 tokens")
            .Produces<object>(StatusCodes.Status200OK);

        endpoints.MapGet("/api/v1/health/live", () => TypedResults.Ok(new { status = "live" }))
            .AllowAnonymous()
            .WithName("HealthLive")
            .WithSummary("Liveness probe");

        endpoints.MapGet("/api/v1/health/ready", async Task<IResult> (AppDbContext db, CancellationToken ct) =>
        {
            var canConnect = await db.Database.CanConnectAsync(ct);
            return canConnect
                ? TypedResults.Ok(new { status = "ready", database = "connected" })
                : TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable);
        })
        .AllowAnonymous()
        .WithName("HealthReady")
        .WithSummary("Readiness probe verifying database connectivity");

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        [FromBody] RegisterRequest request,
        UserManager<AppUser> userManager,
        IEmailQueue emailQueue,
        ISecurityAuditor auditor,
        IConfiguration config,
        HttpContext context,
        CancellationToken ct)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString();
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var webUrl = config["Web:SiteUrl"] ?? "http://localhost:3000";

        // 1. Pre-validate password using configured validators
        var dummyUser = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName
        };

        foreach (var validator in userManager.PasswordValidators)
        {
            var valResult = await validator.ValidateAsync(userManager, dummyUser, request.Password);
            if (!valResult.Succeeded)
            {
                return ApiProblemDetails.CreateResult(
                    StatusCodes.Status400BadRequest,
                    "validation.password",
                    "Password validation failed",
                    valResult.Errors.First().Description,
                    context.Request.Path);
            }
        }

        // 2. Check if user already exists
        var existingUser = await userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            // Anti-enumeration: notify the real owner via email and return 202 without revealing user existence
            var notice = EmailTemplates.AlreadyRegistered(webUrl, request.Email);
            await emailQueue.QueueEmailAsync(notice, ct);
            await auditor.AuditAsync(SecurityEventTypes.RegisterRequested, existingUser.Id, ip, userAgent, new { result = "existing_user" }, ct);

            return TypedResults.StatusCode(StatusCodes.Status202Accepted);
        }

        // 3. Create new user
        var newUser = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            EmailConfirmed = false
        };

        var createResult = await userManager.CreateAsync(newUser, request.Password);
        if (!createResult.Succeeded)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status400BadRequest,
                "validation.failed",
                "Registration failed",
                createResult.Errors.First().Description,
                context.Request.Path);
        }

        await userManager.AddToRoleAsync(newUser, Roles.User);

        // 4. Send email confirmation link with token in URL fragment
        var token = await userManager.GenerateEmailConfirmationTokenAsync(newUser);
        var email = EmailTemplates.ConfirmEmail(webUrl, newUser.Id, token, newUser.Email!);
        await emailQueue.QueueEmailAsync(email, ct);

        await auditor.AuditAsync(SecurityEventTypes.RegisterRequested, newUser.Id, ip, userAgent, new { result = "created" }, ct);

        return TypedResults.StatusCode(StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> ConfirmEmailAsync(
        [FromBody] ConfirmEmailRequest request,
        UserManager<AppUser> userManager,
        ISecurityAuditor auditor,
        HttpContext context,
        CancellationToken ct)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString();
        var userAgent = context.Request.Headers.UserAgent.ToString();

        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user == null)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status400BadRequest,
                AuthErrorCodes.InvalidToken,
                "Invalid confirmation token",
                "The email confirmation token is invalid or has expired.",
                context.Request.Path);
        }

        var token = request.Token?.Trim() ?? string.Empty;
        // Normalize token: Base64 tokens never contain spaces; if an intermediary URL parser converted '+' to ' ', restore '+'
        if (token.Contains(' '))
        {
            token = token.Replace(' ', '+');
        }

        var result = await userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status400BadRequest,
                AuthErrorCodes.InvalidToken,
                "Invalid confirmation token",
                "The email confirmation token is invalid or has expired.",
                context.Request.Path);
        }

        await auditor.AuditAsync(SecurityEventTypes.EmailConfirmed, user.Id, ip, userAgent, null, ct);
        return TypedResults.Ok(new { status = "email_confirmed" });
    }

    private static async Task<IResult> ResendConfirmationAsync(
        [FromBody] ResendConfirmationRequest request,
        UserManager<AppUser> userManager,
        IEmailQueue emailQueue,
        IConfiguration config,
        CancellationToken ct)
    {
        var webUrl = config["Web:SiteUrl"] ?? "http://localhost:3000";

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user != null && !user.EmailConfirmed)
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var email = EmailTemplates.ConfirmEmail(webUrl, user.Id, token, user.Email!);
            await emailQueue.QueueEmailAsync(email, ct);
        }

        return TypedResults.StatusCode(StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> LoginAsync(
        [FromBody] LoginRequest request,
        UserManager<AppUser> userManager,
        RoleManager<AppRole> roleManager,
        ISessionService sessionService,
        ITokenService tokenService,
        ISecurityAuditor auditor,
        IEmailQueue emailQueue,
        AppDbContext db,
        TimeProvider timeProvider,
        IOptions<JwtOptions> jwtOptions,
        HttpContext context,
        CancellationToken ct)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString();
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var now = timeProvider.GetUtcNow();

        var user = await userManager.FindByEmailAsync(request.Email);

        // Constant-time protection: execute dummy password verification if user does not exist
        if (user == null)
        {
            new PasswordHasher<AppUser>().VerifyHashedPassword(
                new AppUser { DisplayName = "Dummy" }, DummyHash, request.Password);

            await auditor.AuditUnknownEmailAsync(SecurityEventTypes.LoginFailed, request.Email, ip, userAgent, new { reason = "user_not_found" }, ct);

            return ApiProblemDetails.CreateResult(
                StatusCodes.Status401Unauthorized,
                AuthErrorCodes.InvalidCredentials,
                "Invalid credentials",
                "The email address or password provided is incorrect.",
                context.Request.Path);
        }

        // Verify password
        var passwordVerification = userManager.PasswordHasher.VerifyHashedPassword(user, user.PasswordHash!, request.Password);

        if (passwordVerification == PasswordVerificationResult.Failed)
        {
            await userManager.AccessFailedAsync(user);
            var isLocked = await userManager.IsLockedOutAsync(user);

            if (isLocked)
            {
                await auditor.AuditAsync(SecurityEventTypes.LockedOut, user.Id, ip, userAgent, null, ct);
                var lockEmail = EmailTemplates.LockedOut(user.Email!);
                await emailQueue.QueueEmailAsync(lockEmail, ct);
            }
            else
            {
                await auditor.AuditAsync(SecurityEventTypes.LoginFailed, user.Id, ip, userAgent, new { reason = "bad_password" }, ct);
            }

            return ApiProblemDetails.CreateResult(
                StatusCodes.Status401Unauthorized,
                AuthErrorCodes.InvalidCredentials,
                "Invalid credentials",
                "The email address or password provided is incorrect.",
                context.Request.Path);
        }

        // Check user lockout status
        if (await userManager.IsLockedOutAsync(user))
        {
            await auditor.AuditAsync(SecurityEventTypes.LoginFailed, user.Id, ip, userAgent, new { reason = "locked_out" }, ct);

            return ApiProblemDetails.CreateResult(
                StatusCodes.Status401Unauthorized,
                AuthErrorCodes.InvalidCredentials,
                "Invalid credentials",
                "The email address or password provided is incorrect.",
                context.Request.Path);
        }

        // Upgrade password hash on login if configured iteration count was increased
        if (passwordVerification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = userManager.PasswordHasher.HashPassword(user, request.Password);
            await userManager.UpdateAsync(user);
        }

        // Check confirmed email requirement
        if (!user.EmailConfirmed)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status403Forbidden,
                AuthErrorCodes.EmailNotConfirmed,
                "Email confirmation required",
                "You must verify your email address before logging in.",
                context.Request.Path);
        }

        // Check if two-factor authentication is enabled
        if (user.TwoFactorEnabled)
        {
            var rawChallenge = SessionService.GenerateOpaqueToken();
            var challengeHash = SessionService.HashToken(rawChallenge);

            var challenge = new MfaChallenge
            {
                Id = Guid.CreateVersion7(),
                UserId = user.Id,
                TokenHash = challengeHash,
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(5),
                AttemptsCount = 0,
                IsConsumed = false,
                RememberMe = request.RememberMe
            };

            db.MfaChallenges.Add(challenge);
            await db.SaveChangesAsync(ct);

            return TypedResults.Ok(new AuthResponse(
                Status: "two_factor_required",
                AccessToken: null,
                ExpiresIn: null,
                RefreshToken: null,
                RefreshExpiresAt: null,
                SessionId: null,
                ChallengeToken: rawChallenge,
                User: null));
        }

        // Login successful (no MFA required)
        await userManager.ResetAccessFailedCountAsync(user);
        user.LastLoginAt = now;
        await userManager.UpdateAsync(user);

        var (session, rawRefreshToken) = await sessionService.CreateSessionAsync(
            user.Id, request.RememberMe, ip, userAgent, mfaVerified: false, ct);

        var roles = await userManager.GetRolesAsync(user);
        var permissions = await GetPermissionsForRolesAsync(roleManager, roles);

        var accessToken = tokenService.GenerateAccessToken(user, session, roles.ToList(), permissions, now);
        var expiresInSeconds = (int)jwtOptions.Value.AccessTokenLifetime.TotalSeconds;

        await auditor.AuditAsync(SecurityEventTypes.LoginSucceeded, user.Id, ip, userAgent, new { mfa = false }, ct);

        var userSummary = new UserSummaryResponse(
            user.Id,
            user.Email!,
            user.DisplayName,
            user.EmailConfirmed,
            user.TwoFactorEnabled,
            roles.ToList());

        return TypedResults.Ok(new AuthResponse(
            Status: "authenticated",
            AccessToken: accessToken,
            ExpiresIn: expiresInSeconds,
            RefreshToken: rawRefreshToken,
            RefreshExpiresAt: session.ExpiresAt,
            SessionId: session.Id,
            ChallengeToken: null,
            User: userSummary));
    }

    private static async Task<IResult> TwoFactorLoginAsync(
        [FromBody] TwoFactorLoginRequest request,
        UserManager<AppUser> userManager,
        RoleManager<AppRole> roleManager,
        ISessionService sessionService,
        ITokenService tokenService,
        ISecurityAuditor auditor,
        AppDbContext db,
        TimeProvider timeProvider,
        IOptions<JwtOptions> jwtOptions,
        HttpContext context,
        CancellationToken ct)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString();
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var now = timeProvider.GetUtcNow();

        var challengeHash = SessionService.HashToken(request.ChallengeToken);
        var challenge = await db.MfaChallenges
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.TokenHash == challengeHash, ct);

        if (challenge == null || challenge.IsConsumed || challenge.ExpiresAt <= now)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status400BadRequest,
                AuthErrorCodes.ChallengeExpired,
                "Challenge expired",
                "The two-factor authentication challenge has expired or is invalid. Please log in again.",
                context.Request.Path);
        }

        if (challenge.AttemptsCount >= 5)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status400BadRequest,
                AuthErrorCodes.ChallengeExceeded,
                "Maximum attempts exceeded",
                "Too many failed two-factor attempts. Please sign in again.",
                context.Request.Path);
        }

        challenge.AttemptsCount++;
        var user = challenge.User;
        var isValid = false;
        var usedRecoveryCode = false;

        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var cleanCode = request.Code.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
            var currentStep = now.ToUnixTimeSeconds() / 30;

            // Replay protection: reject if this exact time step has already been consumed
            if (user.LastTotpStep.HasValue && user.LastTotpStep.Value == currentStep)
            {
                isValid = false;
            }
            else
            {
                isValid = await userManager.VerifyTwoFactorTokenAsync(
                    user, userManager.Options.Tokens.AuthenticatorTokenProvider, cleanCode);

                if (isValid)
                {
                    user.LastTotpStep = currentStep;
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.RecoveryCode))
        {
            var cleanRecoveryCode = request.RecoveryCode.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
            var redeemResult = await userManager.RedeemTwoFactorRecoveryCodeAsync(user, cleanRecoveryCode);
            if (redeemResult.Succeeded)
            {
                isValid = true;
                usedRecoveryCode = true;
            }
        }

        if (!isValid)
        {
            await userManager.AccessFailedAsync(user);
            await db.SaveChangesAsync(ct);

            return ApiProblemDetails.CreateResult(
                StatusCodes.Status400BadRequest,
                usedRecoveryCode ? AuthErrorCodes.InvalidRecoveryCode : AuthErrorCodes.InvalidTotp,
                "Verification failed",
                "The two-factor code or recovery code is invalid.",
                context.Request.Path);
        }

        // Challenge successfully satisfied
        challenge.IsConsumed = true;
        await userManager.ResetAccessFailedCountAsync(user);
        user.LastLoginAt = now;
        await userManager.UpdateAsync(user);

        var (session, rawRefreshToken) = await sessionService.CreateSessionAsync(
            user.Id, challenge.RememberMe, ip, userAgent, mfaVerified: true, ct);

        var roles = await userManager.GetRolesAsync(user);
        var permissions = await GetPermissionsForRolesAsync(roleManager, roles);

        var accessToken = tokenService.GenerateAccessToken(user, session, roles.ToList(), permissions, now);
        var expiresInSeconds = (int)jwtOptions.Value.AccessTokenLifetime.TotalSeconds;

        await auditor.AuditAsync(
            usedRecoveryCode ? SecurityEventTypes.RecoveryCodeUsed : SecurityEventTypes.LoginSucceeded,
            user.Id, ip, userAgent, new { mfa = true, recovery = usedRecoveryCode }, ct);

        var userSummary = new UserSummaryResponse(
            user.Id,
            user.Email!,
            user.DisplayName,
            user.EmailConfirmed,
            user.TwoFactorEnabled,
            roles.ToList());

        return TypedResults.Ok(new AuthResponse(
            Status: "authenticated",
            AccessToken: accessToken,
            ExpiresIn: expiresInSeconds,
            RefreshToken: rawRefreshToken,
            RefreshExpiresAt: session.ExpiresAt,
            SessionId: session.Id,
            ChallengeToken: null,
            User: userSummary));
    }

    private static async Task<IResult> RefreshAsync(
        [FromBody] RefreshTokenRequest request,
        ISessionService sessionService,
        ITokenService tokenService,
        UserManager<AppUser> userManager,
        RoleManager<AppRole> roleManager,
        ISecurityAuditor auditor,
        TimeProvider timeProvider,
        IOptions<JwtOptions> jwtOptions,
        HttpContext context,
        CancellationToken ct)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString();
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var now = timeProvider.GetUtcNow();

        var refreshResult = await sessionService.RefreshSessionAsync(request.RefreshToken, ip, userAgent, ct);

        if (!refreshResult.IsSuccess)
        {
            if (refreshResult.ErrorCode == AuthErrorCodes.RefreshReuse)
            {
                await auditor.AuditAsync(
                    SecurityEventTypes.RefreshReuseDetected,
                    refreshResult.UserId,
                    ip, userAgent,
                    new { sessionId = refreshResult.RevokedSessionId },
                    ct);

                return ApiProblemDetails.CreateResult(
                    StatusCodes.Status401Unauthorized,
                    AuthErrorCodes.RefreshReuse,
                    "Refresh token reuse detected",
                    "Security violation: this refresh token was already used. All active sessions have been revoked.",
                    context.Request.Path);
            }

            if (refreshResult.ErrorCode == AuthErrorCodes.RefreshRace)
            {
                return ApiProblemDetails.CreateResult(
                    StatusCodes.Status401Unauthorized,
                    AuthErrorCodes.RefreshRace,
                    "Concurrent refresh race",
                    "A refresh was already processed within the grace window.",
                    context.Request.Path);
            }

            return ApiProblemDetails.CreateResult(
                StatusCodes.Status401Unauthorized,
                refreshResult.ErrorCode ?? AuthErrorCodes.Unauthenticated,
                "Invalid refresh token",
                "The refresh token is invalid, expired, or revoked.",
                context.Request.Path);
        }

        var session = refreshResult.Session!;
        var user = session.User;
        var roles = await userManager.GetRolesAsync(user);
        var permissions = await GetPermissionsForRolesAsync(roleManager, roles);

        var accessToken = tokenService.GenerateAccessToken(user, session, roles.ToList(), permissions, now);
        var expiresInSeconds = (int)jwtOptions.Value.AccessTokenLifetime.TotalSeconds;

        var userSummary = new UserSummaryResponse(
            user.Id,
            user.Email!,
            user.DisplayName,
            user.EmailConfirmed,
            user.TwoFactorEnabled,
            roles.ToList());

        return TypedResults.Ok(new AuthResponse(
            Status: "authenticated",
            AccessToken: accessToken,
            ExpiresIn: expiresInSeconds,
            RefreshToken: refreshResult.NewRawRefreshToken,
            RefreshExpiresAt: session.ExpiresAt,
            SessionId: session.Id,
            ChallengeToken: null,
            User: userSummary));
    }

    private static async Task<IResult> ForgotPasswordAsync(
        [FromBody] ForgotPasswordRequest request,
        UserManager<AppUser> userManager,
        IEmailQueue emailQueue,
        ISecurityAuditor auditor,
        IConfiguration config,
        HttpContext context,
        CancellationToken ct)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString();
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var webUrl = config["Web:SiteUrl"] ?? "http://localhost:3000";

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user != null)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var email = EmailTemplates.ResetPassword(webUrl, user.Id, token, user.Email!);
            await emailQueue.QueueEmailAsync(email, ct);

            await auditor.AuditAsync(SecurityEventTypes.PasswordResetRequested, user.Id, ip, userAgent, null, ct);
        }
        else
        {
            await auditor.AuditUnknownEmailAsync(SecurityEventTypes.PasswordResetRequested, request.Email, ip, userAgent, null, ct);
        }

        return TypedResults.StatusCode(StatusCodes.Status202Accepted);
    }

    private static async Task<IResult> ResetPasswordAsync(
        [FromBody] ResetPasswordRequest request,
        UserManager<AppUser> userManager,
        ISessionService sessionService,
        IEmailQueue emailQueue,
        ISecurityAuditor auditor,
        HttpContext context,
        CancellationToken ct)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString();
        var userAgent = context.Request.Headers.UserAgent.ToString();

        var user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user == null)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status400BadRequest,
                AuthErrorCodes.InvalidToken,
                "Password reset failed",
                "The reset token is invalid or has expired.",
                context.Request.Path);
        }

        var token = request.Token?.Trim() ?? string.Empty;
        // Normalize token: Base64 tokens never contain spaces; if an intermediary URL parser converted '+' to ' ', restore '+'
        if (token.Contains(' '))
        {
            token = token.Replace(' ', '+');
        }

        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status400BadRequest,
                AuthErrorCodes.InvalidToken,
                "Password reset failed",
                result.Errors.First().Description,
                context.Request.Path);
        }

        // Revoke all sessions on password reset
        await sessionService.RevokeAllUserSessionsAsync(user.Id, "password_reset", null, ct);

        // Send confirmation email
        var notice = EmailTemplates.PasswordChanged(user.Email!);
        await emailQueue.QueueEmailAsync(notice, ct);

        await auditor.AuditAsync(SecurityEventTypes.PasswordReset, user.Id, ip, userAgent, null, ct);

        return TypedResults.Ok(new { status = "password_reset_success" });
    }

    private static async Task<IResult> LogoutAsync(
        ISessionService sessionService,
        ISecurityAuditor auditor,
        ClaimsPrincipal caller,
        HttpContext context,
        CancellationToken ct)
    {
        var sidClaim = caller.FindFirstValue("sid");
        var subClaim = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.FindFirstValue("sub");

        if (Guid.TryParse(sidClaim, out var sessionId))
        {
            await sessionService.RevokeSessionAsync(sessionId, "user_logout", ct);
            var userId = Guid.TryParse(subClaim, out var u) ? u : (Guid?)null;
            await auditor.AuditAsync(
                SecurityEventTypes.SessionRevoked,
                userId,
                context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers.UserAgent.ToString(),
                new { sessionId },
                ct);
        }

        return TypedResults.Ok(new { status = "logged_out" });
    }

    private static async Task<IResult> LogoutAllAsync(
        ISessionService sessionService,
        ISecurityAuditor auditor,
        ClaimsPrincipal caller,
        HttpContext context,
        CancellationToken ct)
    {
        var subClaim = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.FindFirstValue("sub");

        if (Guid.TryParse(subClaim, out var userId))
        {
            await sessionService.RevokeAllUserSessionsAsync(userId, "user_logout_all", null, ct);
            await auditor.AuditAsync(
                SecurityEventTypes.SessionRevoked,
                userId,
                context.Connection.RemoteIpAddress?.ToString(),
                context.Request.Headers.UserAgent.ToString(),
                new { all = true },
                ct);
        }

        return TypedResults.Ok(new { status = "all_sessions_revoked" });
    }

    private static IResult GetJwks(IKeyManagementService keyManagementService) =>
        TypedResults.Ok(keyManagementService.GetJwks());

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
