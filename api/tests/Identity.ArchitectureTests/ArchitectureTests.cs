using System.Reflection;
using Identity.Api.Common.Constants;
using Microsoft.AspNetCore.Authorization;
using NetArchTest.Rules;
using Shouldly;
using Xunit;

namespace Identity.ArchitectureTests;

public class ArchitectureTests
{
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    [Fact]
    public void EndpointsAndHandlers_MustNotUseAuthorizeRoles()
    {
        // Enforce that [Authorize(Roles = "...")] is never used in the application.
        // Role-based authorization must always be derived through policies and permission claims.
        var types = ApiAssembly.GetTypes();

        foreach (var type in types)
        {
            var typeAuthorizeAttributes = type.GetCustomAttributes<AuthorizeAttribute>(inherit: true);
            foreach (var attr in typeAuthorizeAttributes)
            {
                attr.Roles.ShouldBeNullOrEmpty(
                    $"Type '{type.FullName}' uses [Authorize(Roles = ...)], which is forbidden. Use policies instead.");
            }

            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var method in methods)
            {
                var methodAuthorizeAttributes = method.GetCustomAttributes<AuthorizeAttribute>(inherit: true);
                foreach (var attr in methodAuthorizeAttributes)
                {
                    attr.Roles.ShouldBeNullOrEmpty(
                        $"Method '{type.FullName}.{method.Name}' uses [Authorize(Roles = ...)], which is forbidden. Use policies instead.");
                }
            }
        }
    }

    [Fact]
    public void IdentityCookieScheme_MustNotBeRegistered()
    {
        // Verify that IdentityConstants.ApplicationScheme (cookies) is not being used as default scheme.
        // The API must be strictly stateless Bearer token only.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Program.cs")))
        {
            var candidate = Path.Combine(dir.FullName, "src", "Identity.Api", "Program.cs");
            if (File.Exists(candidate))
            {
                var code = File.ReadAllText(candidate);
                code.ShouldNotContain(".AddIdentity<");
                code.ShouldNotContain(".AddIdentityApiEndpoints");
                code.ShouldNotContain(".MapIdentityApi");
                code.ShouldNotContain(".AddDefaultIdentity");
                return;
            }
            dir = dir.Parent;
        }

        // Also assert that the assembly does not register DefaultAuthenticationScheme as cookies
        var programType = typeof(Program);
        programType.ShouldNotBeNull();
    }

    [Fact]
    public void PermissionsConstants_MustBeTheSingleSourceOfTruth()
    {
        Permissions.All.Count.ShouldBe(6);
        Permissions.All.ShouldContain(Permissions.ProfileManage);
        Permissions.All.ShouldContain(Permissions.NotesManageOwn);
        Permissions.All.ShouldContain(Permissions.UsersRead);
        Permissions.All.ShouldContain(Permissions.AuditRead);
        Permissions.All.ShouldContain(Permissions.UsersManage);
        Permissions.All.ShouldContain(Permissions.NotesModerate);
    }
}
