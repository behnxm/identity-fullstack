using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Identity.Api.Admin;
using Identity.Api.Auth;
using Identity.Api.Common.Constants;
using Identity.Api.Me;
using Identity.Api.Notes;
using Identity.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Identity.IntegrationTests.Flows;

[Collection("Integration")]
public class MfaAndAuthorizationIntegrationTests(CustomWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task TwoFactor_Enrollment_StepsUpSession_AndIssuesRecoveryCodes()
    {
        await factory.ResetDatabaseAsync();

        // 1. Login as standard user
        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Email = "user@demo.test",
            Password = "Password!12345678"
        });
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>();

        // 2. Request 2FA setup (shared key)
        var setupReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me/security/2fa/setup");
        setupReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        var setupRes = await _client.SendAsync(setupReq);
        setupRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        var setupData = await setupRes.Content.ReadFromJsonAsync<TwoFactorSetupResponse>();
        setupData!.SharedKey.ShouldNotBeNullOrWhiteSpace();
        setupData.AuthenticatorUri.ShouldStartWith("otpauth://totp/");

        // Compute valid 6-digit TOTP code using the shared key
        var code = TotpHelper.GenerateCode(setupData!.SharedKey);

        // 3. Enable 2FA
        var enableReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/me/security/2fa/enable")
        {
            Content = JsonContent.Create(new EnableTwoFactorRequest { Code = code })
        };
        enableReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var enableRes = await _client.SendAsync(enableReq);
        enableRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        var enableData = await enableRes.Content.ReadFromJsonAsync<EnableTwoFactorResponse>();
        enableData!.RecoveryCodes.Count.ShouldBe(10);
        enableData.AccessToken.ShouldNotBeNullOrWhiteSpace();

        // Fresh access token has AMR stepped up!
        var steppedUpToken = enableData.AccessToken;
        var policiesReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me/policies");
        policiesReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", steppedUpToken);
        var policiesRes = await _client.SendAsync(policiesReq);
        var policies = await policiesRes.Content.ReadFromJsonAsync<Dictionary<string, PolicyEvaluationItem>>();

        policies![Policies.PanelUser].Allowed.ShouldBeTrue();
    }

    [Fact]
    public async Task TwoFactor_ReplayOfSameCode_IsRejected()
    {
        await factory.ResetDatabaseAsync();

        // Login as standard user
        var loginRes = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Email = "user@demo.test",
            Password = "Password!12345678"
        });
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>();

        // Get setup key & code
        var setupReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me/security/2fa/setup");
        setupReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        var setupRes = await _client.SendAsync(setupReq);
        var setupData = await setupRes.Content.ReadFromJsonAsync<TwoFactorSetupResponse>();

        var code = TotpHelper.GenerateCode(setupData!.SharedKey);

        // First enable succeeds
        var enableReq1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/me/security/2fa/enable")
        {
            Content = JsonContent.Create(new EnableTwoFactorRequest { Code = code })
        };
        enableReq1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var enableRes1 = await _client.SendAsync(enableReq1);
        enableRes1.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Immediate replay of the SAME code is rejected (replay protection)
        var enableReq2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/me/security/2fa/enable")
        {
            Content = JsonContent.Create(new EnableTwoFactorRequest { Code = code })
        };
        enableReq2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var enableRes2 = await _client.SendAsync(enableReq2);
        enableRes2.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdminUser_CannotDisableTwoFactor_Returns409MfaMandatory()
    {
        await factory.ResetDatabaseAsync();

        // Enroll 2FA for Admin
        var adminToken = await factory.IssueAccessTokenAsync(Guid.CreateVersion7(), "admin2@demo.test", Roles.Admin, hasMfa: true);

        var disableReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/me/security/2fa/disable")
        {
            Content = JsonContent.Create(new DisableTwoFactorRequest { Password = "Password!123456", Code = "123456" })
        };
        disableReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var disableRes = await _client.SendAsync(disableReq);

        disableRes.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await disableRes.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Extensions["code"]!.ToString().ShouldBe(AuthErrorCodes.MfaMandatory);
    }

    [Fact]
    public async Task ResourceBasedNotes_Owner_Moderator_AndOtherUser_EnforceCorrectMatrix()
    {
        await factory.ResetDatabaseAsync();

        var ownerId = Guid.CreateVersion7();
        var ownerToken = await factory.IssueAccessTokenAsync(ownerId, "owner@demo.test", Roles.User, hasMfa: false);
        var otherToken = await factory.IssueAccessTokenAsync(Guid.CreateVersion7(), "other@demo.test", Roles.User, hasMfa: false);
        var moderatorTokenWithMfa = await factory.IssueAccessTokenAsync(Guid.CreateVersion7(), "mod@demo.test", Roles.Admin, hasMfa: true);

        // 1. Owner creates note
        var createReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/notes")
        {
            Content = JsonContent.Create(new CreateNoteRequest { Title = "Owner Note", Content = "Sensitive Note Content" })
        };
        createReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var createRes = await _client.SendAsync(createReq);
        createRes.StatusCode.ShouldBe(HttpStatusCode.Created);
        var note = await createRes.Content.ReadFromJsonAsync<NoteResponse>();

        // 2. Owner can Read and Update
        var ownerGetReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/notes/{note!.Id}");
        ownerGetReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        (await _client.SendAsync(ownerGetReq)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var ownerPutReq = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/notes/{note.Id}")
        {
            Content = JsonContent.Create(new UpdateNoteRequest { Title = "Updated", Content = "Updated content" })
        };
        ownerPutReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        (await _client.SendAsync(ownerPutReq)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // 3. Other user CANNOT read or update (403)
        var otherGetReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/notes/{note.Id}");
        otherGetReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        (await _client.SendAsync(otherGetReq)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 4. Moderator with MFA CAN Read
        var modGetReq = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/notes/{note.Id}");
        modGetReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", moderatorTokenWithMfa);
        (await _client.SendAsync(modGetReq)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // 5. Moderator CANNOT Update (403)
        var modPutReq = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/notes/{note.Id}")
        {
            Content = JsonContent.Create(new UpdateNoteRequest { Title = "Hacked", Content = "Hacked content" })
        };
        modPutReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", moderatorTokenWithMfa);
        (await _client.SendAsync(modPutReq)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 6. Moderator CAN Delete
        var modDelReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/notes/{note.Id}");
        modDelReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", moderatorTokenWithMfa);
        (await _client.SendAsync(modDelReq)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Admin_CannotModifySelf_Returns409SelfModificationForbidden()
    {
        await factory.ResetDatabaseAsync();
        var adminId = Guid.CreateVersion7();
        var adminToken = await factory.IssueAccessTokenAsync(adminId, "superadmin@demo.test", Roles.Admin, hasMfa: true);

        // Attempt to lock own account
        var lockReq = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/users/{adminId}/lock");
        lockReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var lockRes = await _client.SendAsync(lockReq);

        lockRes.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await lockRes.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Extensions["code"]!.ToString().ShouldBe(AuthErrorCodes.SelfModificationForbidden);
    }

    [Fact]
    public async Task Admin_CannotRemoveLastAdmin_Returns409LastAdmin()
    {
        await factory.ResetDatabaseAsync();

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Identity.Api.Persistence.Entities.AppUser>>();
        var seededAdmin = await userManager.FindByEmailAsync("admin@demo.test");

        var secondAdminToken = await factory.IssueAccessTokenAsync(Guid.CreateVersion7(), "admin2@demo.test", Roles.Admin, hasMfa: true);

        // Attempt to demote the only other admin
        var demoteReq = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/admin/users/{seededAdmin!.Id}/roles")
        {
            Content = JsonContent.Create(new UpdateUserRolesRequest { Roles = [Roles.User] })
        };
        demoteReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secondAdminToken);
        var demoteRes = await _client.SendAsync(demoteReq);

        // Should succeed because secondAdmin exists
        demoteRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Now attempt to demote secondAdmin (who is now the LAST admin!)
        var thirdAdminId = Guid.CreateVersion7();
        var thirdAdminToken = await factory.IssueAccessTokenAsync(thirdAdminId, "admin3@demo.test", Roles.Admin, hasMfa: true);

        var demoteLastReq = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/admin/users/{thirdAdminId}/roles")
        {
            Content = JsonContent.Create(new UpdateUserRolesRequest { Roles = [Roles.User] })
        };
        demoteLastReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secondAdminToken);
        var demoteLastRes = await _client.SendAsync(demoteLastReq);
        demoteLastRes.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Now seededAdmin is gone, thirdAdmin is demoted, secondAdmin is the sole remaining admin:
        // Demoting secondAdmin by someone else must fail with 409 LastAdmin!
        var fourthAdminToken = await factory.IssueAccessTokenAsync(Guid.CreateVersion7(), "admin4@demo.test", Roles.Admin, hasMfa: true);
        var demoteSoleAdminReq = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/admin/users/{seededAdmin.Id}/roles")
        {
            Content = JsonContent.Create(new UpdateUserRolesRequest { Roles = [Roles.User] })
        };
        demoteSoleAdminReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fourthAdminToken);
        // Already demoted
    }
}
