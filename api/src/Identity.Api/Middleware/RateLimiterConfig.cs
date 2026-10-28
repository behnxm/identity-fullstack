using System.Net;
using System.Threading.RateLimiting;
using Identity.Api.Common.Constants;
using Identity.Api.Common.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Identity.Api.Middleware;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";
    public int LoginPermitLimit { get; set; } = 10;
    public int SensitivePermitLimit { get; set; } = 5;
    public int RefreshPermitLimit { get; set; } = 30;
    public int GlobalPermitLimit { get; set; } = 100;
}

public static class RateLimiterConfig
{
    public const string AuthLoginPolicy = "auth-login";
    public const string AuthSensitivePolicy = "auth-sensitive";
    public const string AuthRefreshPolicy = "auth-refresh";
    public const string GlobalPolicy = "global";

    public static IServiceCollection AddSecurityRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }

                var problem = ApiProblemDetails.Create(
                    StatusCodes.Status429TooManyRequests,
                    AuthErrorCodes.RateLimited,
                    "Too Many Requests",
                    "You have exceeded the rate limit. Please try again later.",
                    context.HttpContext.Request.Path);

                await context.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken: token);
            };

            // Login rate limiter
            options.AddPolicy(AuthLoginPolicy, httpContext =>
            {
                var limits = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    GetClientIpKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.LoginPermitLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            // Sensitive actions (register, forgot-password, resend-confirmation)
            options.AddPolicy(AuthSensitivePolicy, httpContext =>
            {
                var limits = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    GetClientIpKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.SensitivePermitLimit,
                        Window = TimeSpan.FromMinutes(10),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            // Refresh token endpoint
            options.AddPolicy(AuthRefreshPolicy, httpContext =>
            {
                var limits = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    GetClientIpKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.RefreshPermitLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            // Global fallback rate limiter
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var limits = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    GetClientIpKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.GlobalPermitLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });
        });

        return services;
    }

    public static string GetClientIpKey(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) && !string.IsNullOrWhiteSpace(forwardedFor))
        {
            var firstIp = forwardedFor.ToString().Split(',', StringSplitOptions.TrimEntries)[0];
            if (!string.IsNullOrWhiteSpace(firstIp))
            {
                return firstIp;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
