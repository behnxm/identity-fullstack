using Microsoft.AspNetCore.Http;

namespace Identity.Api.Middleware;

/// <summary>
/// Hardens HTTP responses with security headers and applies no-store caching on sensitive endpoints.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Add security response headers
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-Frame-Options"] = "DENY";

        // Enforce no-store on sensitive auth and user endpoints
        if (context.Request.Path.StartsWithSegments("/api/v1/auth") ||
            context.Request.Path.StartsWithSegments("/api/v1/me") ||
            context.Request.Path.StartsWithSegments("/api/v1/admin"))
        {
            context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
            context.Response.Headers.Pragma = "no-cache";
        }

        await next(context);
    }
}
