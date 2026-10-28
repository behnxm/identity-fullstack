using Identity.Api.Common.Constants;
using Identity.Api.Common.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace Identity.Api.Security.Authorization;

/// <summary>
/// Custom AuthorizationMiddlewareResultHandler returning RFC 9457 Problem Details.
/// Formats 401 with WWW-Authenticate, 403 auth.mfa_required when MfaRequirement failed,
/// and 403 auth.forbidden without leaking missing permission details.
/// </summary>
public sealed class SecurityAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Challenged)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.Append("WWW-Authenticate", "Bearer");

            var problem = ApiProblemDetails.Create(
                StatusCodes.Status401Unauthorized,
                AuthErrorCodes.Unauthenticated,
                "Authentication required",
                "You must supply a valid bearer access token to access this resource.",
                context.Request.Path);

            await context.Response.WriteAsJsonAsync(problem);
            return;
        }

        if (authorizeResult.Forbidden)
        {
            var isMfaFailure = authorizeResult.AuthorizationFailure?.FailedRequirements
                .Any(r => r is MfaRequirement) == true;

            var statusCode = StatusCodes.Status403Forbidden;
            var errorCode = isMfaFailure ? AuthErrorCodes.MfaRequired : AuthErrorCodes.Forbidden;
            var title = isMfaFailure ? "Two-factor authentication required" : "Forbidden";
            var detail = isMfaFailure
                ? "This operation or administrative panel requires two-factor authentication."
                : "You do not have permission to access this resource.";

            context.Response.StatusCode = statusCode;

            var problem = ApiProblemDetails.Create(
                statusCode,
                errorCode,
                title,
                detail,
                context.Request.Path);

            await context.Response.WriteAsJsonAsync(problem);
            return;
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
