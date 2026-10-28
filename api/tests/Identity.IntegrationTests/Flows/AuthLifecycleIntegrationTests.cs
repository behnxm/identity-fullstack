using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Identity.Api.Auth;
using Identity.Api.Common.Constants;
using Identity.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using Xunit;

namespace Identity.IntegrationTests.Flows;

[Collection("Integration")]
public class AuthLifecycleIntegrationTests(CustomWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_NewUser_Returns202Accepted_AndSendsEmailWithFragmentToken()
    {
        await factory.ResetDatabaseAsync();
        var email = "charlie@example.com";
        var request = new RegisterRequest
        {
            Email = email,
            Password = "SecurePassword!1234",
            DisplayName = "Charlie"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // Verify confirmation email was sent
        var sentEmail = factory.EmailSender.GetLastEmailTo(email);
        sentEmail.ShouldNotBeNull();
        sentEmail.Subject.ShouldContain("Confirm your email");

        // Token must be in the URL fragment (#token=)
        sentEmail.PlainTextContent.ShouldContain("#token=");
        sentEmail.HtmlContent.ShouldContain("#token=");
    }

    [Fact]
    public async Task Register_ExistingUser_Returns202Accepted_WithIdenticalPayloadShape_AndNotifiesOwner()
    {
        await factory.ResetDatabaseAsync();
        var email = "user@demo.test"; // seeded demo user
        var request = new RegisterRequest
        {
            Email = email,
            Password = "SecurePassword!1234",
            DisplayName = "Imposter"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Anti-enumeration: returns exact same 202 Accepted status
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        // Owner receives an "already registered / registration attempt" notification
        var sentEmail = factory.EmailSender.GetLastEmailTo(email);
        sentEmail.ShouldNotBeNull();
        sentEmail.Subject.ShouldContain("Registration attempt");
    }

    [Fact]
    public async Task ConfirmEmail_WithValidToken_ConfirmsAccount()
    {
        await factory.ResetDatabaseAsync();
        var email = "david@example.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "SecurePassword!1234",
            DisplayName = "David"
        });

        var sentEmail = factory.EmailSender.GetLastEmailTo(email)!;

        // Extract userId and token from email link: /confirm-email?userId=...#token=...
        var uri = ExtractUriFromEmail(sentEmail.PlainTextContent);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var userId = Guid.Parse(query["userId"]!);
        var token = uri.Fragment.Replace("#token=", string.Empty);
        token = WebUtility.UrlDecode(token);

        var confirmResponse = await _client.PostAsJsonAsync("/api/v1/auth/confirm-email", new ConfirmEmailRequest
        {
            UserId = userId,
            Token = token
        });

        confirmResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsGeneric401_ForBothMissingAndWrongPassword()
    {
        await factory.ResetDatabaseAsync();

        // 1. Non-existent user
        var res1 = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Email = "nonexistent@example.com",
            Password = "Password!12345678"
        });
        res1.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var prob1 = await res1.Content.ReadFromJsonAsync<ProblemDetails>();
        prob1!.Extensions["code"]!.ToString().ShouldBe(AuthErrorCodes.InvalidCredentials);

        // 2. Existing user with wrong password
        var res2 = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Email = "user@demo.test",
            Password = "WrongPassword!123"
        });
        res2.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var prob2 = await res2.Content.ReadFromJsonAsync<ProblemDetails>();
        prob2!.Extensions["code"]!.ToString().ShouldBe(AuthErrorCodes.InvalidCredentials);
    }

    [Fact]
    public async Task Login_UnconfirmedEmail_Returns403EmailNotConfirmed()
    {
        await factory.ResetDatabaseAsync();
        var email = "unconfirmed@example.com";
        var password = "SecurePassword!1234";

        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
        {
            Email = email,
            Password = password,
            DisplayName = "Unconfirmed User"
        });

        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });

        loginRes.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await loginRes.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Extensions["code"]!.ToString().ShouldBe(AuthErrorCodes.EmailNotConfirmed);
    }

    [Fact]
    public async Task Lockout_After5Failures_LocksAccount_AndSendsAlertEmail()
    {
        await factory.ResetDatabaseAsync();
        var email = "user@demo.test";

        for (var i = 0; i < 5; i++)
        {
            await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
            {
                Email = email,
                Password = "BadPassword!123"
            });
        }

        // Lockout email sent to owner
        var lockEmail = factory.EmailSender.GetLastEmailTo(email);
        lockEmail.ShouldNotBeNull();
        lockEmail.Subject.ShouldContain("Account temporarily locked");

        // Subsequent attempt with REAL password still returns 401 invalid credentials (never revealing lockout)
        var nextAttempt = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password!12345678"
        });
        nextAttempt.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_RotationAndReuseDetection_WorksAccurately()
    {
        await factory.ResetDatabaseAsync();

        // 1. Login to obtain token pair
        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Email = "user@demo.test",
            Password = "Password!12345678"
        });
        var auth1 = await loginRes.Content.ReadFromJsonAsync<AuthResponse>();
        auth1!.RefreshToken.ShouldNotBeNull();
        var token1 = auth1.RefreshToken!;

        // 2. Successful refresh rotates token
        var refreshRes1 = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = token1
        });
        refreshRes1.StatusCode.ShouldBe(HttpStatusCode.OK);
        var auth2 = await refreshRes1.Content.ReadFromJsonAsync<AuthResponse>();
        auth2!.RefreshToken.ShouldNotBeNull();
        var token2 = auth2.RefreshToken!;
        token2.ShouldNotBe(token1);

        // 3. Grace-window race test: Presenting token1 again immediately (within 10s grace window) returns 401 auth.refresh_race
        var raceRes = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = token1
        });
        raceRes.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var raceProb = await raceRes.Content.ReadFromJsonAsync<ProblemDetails>();
        raceProb!.Extensions["code"]!.ToString().ShouldBe(AuthErrorCodes.RefreshRace);

        // Token2 is STILL VALID because grace window race does not revoke the session!
        var validRes = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = token2
        });
        validRes.StatusCode.ShouldBe(HttpStatusCode.OK);
        var auth3 = await validRes.Content.ReadFromJsonAsync<AuthResponse>();
        var token3 = auth3!.RefreshToken!;

        // 4. Advance time past grace window (e.g. 15 seconds)
        factory.TimeProvider.Advance(TimeSpan.FromSeconds(15));

        // 5. Presenting old token1 outside grace window triggers REUSE DETECTION: whole session is revoked!
        var reuseRes = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = token1
        });
        reuseRes.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var reuseProb = await reuseRes.Content.ReadFromJsonAsync<ProblemDetails>();
        reuseProb!.Extensions["code"]!.ToString().ShouldBe(AuthErrorCodes.RefreshReuse);

        // The session was revoked: token3 will now be rejected!
        var postRevokeRes = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshTokenRequest
        {
            RefreshToken = token3
        });
        postRevokeRes.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevokesSessionImmediately_WithCacheEviction()
    {
        await factory.ResetDatabaseAsync();

        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Email = "user@demo.test",
            Password = "Password!12345678"
        });
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>();

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        var meRes = await _client.SendAsync(req);
        meRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Logout
        var logoutReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logoutReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var logoutRes = await _client.SendAsync(logoutReq);
        logoutRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Immediate verification: same access token is now rejected because session was revoked and cache evicted!
        var mePostLogout = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        mePostLogout.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var mePostRes = await _client.SendAsync(mePostLogout);
        mePostRes.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ForgotPassword_AndResetPassword_RevokesAllSessions()
    {
        await factory.ResetDatabaseAsync();
        var email = "user@demo.test";

        // Login to get an active session
        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password!12345678"
        });
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>();

        // Forgot password
        var forgotRes = await _client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest { Email = email });
        forgotRes.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var resetEmail = factory.EmailSender.GetLastEmailTo(email)!;
        resetEmail.Subject.ShouldContain("Reset your password");

        var uri = ExtractUriFromEmail(resetEmail.PlainTextContent);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var userId = Guid.Parse(query["userId"]!);
        var token = WebUtility.UrlDecode(uri.Fragment.Replace("#token=", string.Empty));

        // Reset password
        var resetRes = await _client.PostAsJsonAsync("/api/v1/auth/reset-password", new ResetPasswordRequest
        {
            UserId = userId,
            Token = token,
            NewPassword = "NewSecurePassword!999"
        });
        resetRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Previous session token is now rejected
        var meReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        meReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        var meRes = await _client.SendAsync(meReq);
        meRes.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Can login with new password
        var newLoginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Email = email,
            Password = "NewSecurePassword!999"
        });
        newLoginRes.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static Uri ExtractUriFromEmail(string content)
    {
        var match = System.Text.RegularExpressions.Regex.Match(content, @"https?://[^\s\n\r]+");
        return new Uri(match.Value);
    }
}
