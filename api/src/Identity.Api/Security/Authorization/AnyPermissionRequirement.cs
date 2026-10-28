using Microsoft.AspNetCore.Authorization;

namespace Identity.Api.Security.Authorization;

/// <summary>
/// Requirement satisfied if the caller possesses at least ONE of the specified permissions.
/// </summary>
public sealed class AnyPermissionRequirement(params string[] permissions) : IAuthorizationRequirement
{
    public IReadOnlyList<string> Permissions { get; } = permissions;
}

public sealed class AnyPermissionRequirementHandler : AuthorizationHandler<AnyPermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AnyPermissionRequirement requirement)
    {
        var userPermissions = context.User.FindAll("permission")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (requirement.Permissions.Any(p => userPermissions.Contains(p)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
