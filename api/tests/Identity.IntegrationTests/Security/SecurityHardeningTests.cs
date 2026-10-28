using System.Net;
using System.Net.Http.Json;
using Identity.Api.Auth;
using Identity.Api.Common.Constants;
using Identity.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace Identity.IntegrationTests.Security;

[Collection("Integration")]
public class SecurityHardeningTests(CustomWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RateLimiting_WhenThresholdExceeded_Returns429WithRetryAfterAndProblemDetails()
    {
        await factory.ResetDatabaseAsync();

        using var testFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:SensitivePermitLimit"] = "3"
                });
            });
        });

        using var client = testFactory.CreateClient();

        // Sensitive policy permit limit is set to 3 for this test
        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 5; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/forgot-password")
            {
                Content = JsonContent.Create(new ForgotPasswordRequest { Email = $"spam{i}@example.com" })
            };
            req.Headers.Add("X-Forwarded-For", "203.0.113.199"); // Fixed forwarded client IP
            lastResponse = await client.SendAsync(req);
        }

        lastResponse.ShouldNotBeNull();
        lastResponse.StatusCode.ShouldBe((HttpStatusCode)429);
        lastResponse.Headers.Contains("Retry-After").ShouldBeTrue();

        var problem = await lastResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Extensions["code"]!.ToString().ShouldBe(AuthErrorCodes.RateLimited);
    }

    [Fact]
    public async Task SecurityHeaders_ArePresentOnAllResponses()
    {
        var response = await _client.GetAsync("/api/v1/health/live");

        response.Headers.Contains("X-Content-Type-Options").ShouldBeTrue();
        response.Headers.GetValues("X-Content-Type-Options").First().ShouldBe("nosniff");

        response.Headers.Contains("Referrer-Policy").ShouldBeTrue();
        response.Headers.GetValues("Referrer-Policy").First().ShouldBe("no-referrer");

        response.Headers.Contains("X-Frame-Options").ShouldBeTrue();
        response.Headers.GetValues("X-Frame-Options").First().ShouldBe("DENY");
    }

    [Fact]
    public async Task SensitiveAuthEndpoints_ContainNoStoreCacheControl()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Email = "user@demo.test",
            Password = "WrongPassword!123"
        });

        response.Headers.CacheControl.ShouldNotBeNull();
        response.Headers.CacheControl.NoStore.ShouldBeTrue();
    }

    [Fact]
    public async Task LogScrubbing_AssertsNoPasswordsOrTokensAppearInApplicationLogs()
    {
        await factory.ResetDatabaseAsync();

        var sensitivePassword = "SuperSecretPlainTextPassword!2026";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
        {
            Email = "logger_test@example.com",
            Password = sensitivePassword,
            DisplayName = "Logger Test"
        });

        var logs = factory.LogProvider.Logs;

        foreach (var log in logs)
        {
            log.ShouldNotContain(sensitivePassword);
            log.ShouldNotContain("SuperSecretPlainTextPassword");
        }
    }
}
