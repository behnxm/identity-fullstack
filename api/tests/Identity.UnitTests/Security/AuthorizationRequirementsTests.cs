using System.Security.Claims;
using Identity.Api.Common.Constants;
using Identity.Api.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Shouldly;
using Xunit;

namespace Identity.UnitTests.Security;

public class AuthorizationRequirementsTests
{
    [Theory]
    [InlineData("otp", true)]
    [InlineData("mfa", true)]
    [InlineData("pwd", false)]
    [InlineData("sms", false)]
    public async Task MfaRequirementHandler_EvaluatesAmrClaimCorrectly(string amrValue, bool shouldSucceed)
    {
        var handler = new MfaRequirementHandler();
        var requirement = new MfaRequirement();

        var claims = new[] { new Claim("amr", amrValue) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBe(shouldSucceed);
    }

    [Fact]
    public async Task AnyPermissionRequirementHandler_SucceedsIfUserHasAnyMatchingPermission()
    {
        var handler = new AnyPermissionRequirementHandler();
        var requirement = new AnyPermissionRequirement(Permissions.UsersRead, Permissions.AuditRead);

        var claims = new[]
        {
            new Claim("permission", Permissions.UsersRead),
            new Claim("permission", Permissions.ProfileManage)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task AnyPermissionRequirementHandler_FailsIfUserLacksAllMatchingPermissions()
    {
        var handler = new AnyPermissionRequirementHandler();
        var requirement = new AnyPermissionRequirement(Permissions.UsersManage, Permissions.NotesModerate);

        var claims = new[]
        {
            new Claim("permission", Permissions.UsersRead),
            new Claim("permission", Permissions.ProfileManage)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }
}
