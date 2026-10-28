using System.Security.Claims;
using Identity.Api.Common.Constants;
using Identity.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Identity.Api.Security.Authorization;

public static class NoteOperations
{
    public static readonly OperationAuthorizationRequirement Read = new() { Name = nameof(Read) };
    public static readonly OperationAuthorizationRequirement Update = new() { Name = nameof(Update) };
    public static readonly OperationAuthorizationRequirement Delete = new() { Name = nameof(Delete) };
}

/// <summary>
/// Resource-based authorization handler for Note entities.
/// Owner may Read, Update, and Delete.
/// Moderators holding 'notes.moderate' with active MFA may Read and Delete only.
/// </summary>
public sealed class NoteAccessAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, Note>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        Note resource)
    {
        var subClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");

        if (!Guid.TryParse(subClaim, out var userId))
        {
            return Task.CompletedTask;
        }

        // 1. Owner has full permissions (Read, Update, Delete)
        if (resource.OwnerId == userId)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // 2. Moderator with MFA can Read and Delete (cannot Update)
        var hasModeratePermission = context.User.FindAll("permission")
            .Any(c => string.Equals(c.Value, Permissions.NotesModerate, StringComparison.OrdinalIgnoreCase));

        var hasMfa = context.User.FindAll("amr")
            .Any(c => string.Equals(c.Value, "otp", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(c.Value, "mfa", StringComparison.OrdinalIgnoreCase));

        if (hasModeratePermission && hasMfa)
        {
            if (requirement.Name == nameof(NoteOperations.Read) || requirement.Name == nameof(NoteOperations.Delete))
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
