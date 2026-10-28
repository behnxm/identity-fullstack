using System.Security.Claims;
using Identity.Api.Auditing;
using Identity.Api.Common.Constants;
using Identity.Api.Common.Options;
using Identity.Api.Common.Results;
using Identity.Api.Email;
using Identity.Api.Notes;
using Identity.Api.Persistence;
using Identity.Api.Persistence.Entities;
using Identity.Api.Security.Sessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Identity.Api.Admin;

/// <summary>
/// Exposes administrative management, statistics, role inspection, and security audit endpoints under /api/v1/admin.
/// </summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/admin")
            .WithTags("Admin")
            .RequireAuthorization();

        group.MapGet("/stats", GetStatsAsync)
            .RequireAuthorization(Policies.PanelAdmin)
            .WithName("GetAdminStats")
            .WithSummary("System security dashboard statistics")
            .Produces<AdminStatsResponse>(StatusCodes.Status200OK);

        group.MapGet("/users", GetUsersAsync)
            .RequireAuthorization(Policies.UsersRead)
            .WithName("GetUsers")
            .WithSummary("Query users with search, role filters, and pagination")
            .Produces<PagedResult<AdminUserResponse>>(StatusCodes.Status200OK);

        group.MapGet("/users/{id:guid}", GetUserByIdAsync)
            .RequireAuthorization(Policies.UsersRead)
            .WithName("GetUserById")
            .WithSummary("Get detailed user information")
            .Produces<AdminUserResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPost("/users/{id:guid}/lock", LockUserAsync)
            .RequireAuthorization(Policies.UsersManage)
            .WithName("LockUser")
            .WithSummary("Temporarily lock user account and revoke their sessions")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/users/{id:guid}/unlock", UnlockUserAsync)
            .RequireAuthorization(Policies.UsersManage)
            .WithName("UnlockUser")
            .WithSummary("Unlock previously locked user account")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPut("/users/{id:guid}/roles", UpdateUserRolesAsync)
            .RequireAuthorization(Policies.UsersManage)
            .WithName("UpdateUserRoles")
            .WithSummary("Update user role assignments and revoke their sessions")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/users/{id:guid}/revoke-sessions", RevokeUserSessionsAsync)
            .RequireAuthorization(Policies.UsersManage)
            .WithName("AdminRevokeUserSessions")
            .WithSummary("Revoke all active sessions for a target user")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        group.MapPost("/users/{id:guid}/resend-confirmation", ResendUserConfirmationAsync)
            .RequireAuthorization(Policies.UsersManage)
            .WithName("AdminResendUserConfirmation")
            .WithSummary("Trigger email confirmation dispatch for user")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapGet("/roles", GetRolesAsync)
            .RequireAuthorization(Policies.UsersRead)
            .WithName("GetRoles")
            .WithSummary("List all system roles and their assigned permission claims")
            .Produces<IReadOnlyList<RoleResponse>>(StatusCodes.Status200OK);

        group.MapGet("/audit-events", GetAuditEventsAsync)
            .RequireAuthorization(Policies.AuditRead)
            .WithName("GetAuditEvents")
            .WithSummary("Query security audit log with filters and pagination")
            .Produces<PagedResult<SecurityEventResponse>>(StatusCodes.Status200OK);

        group.MapGet("/notes", GetNotesForModerationAsync)
            .RequireAuthorization(Policies.NotesModerate)
            .WithName("GetNotesForModeration")
            .WithSummary("Query all notes across users for moderation purposes")
            .Produces<IReadOnlyList<NoteResponse>>(StatusCodes.Status200OK);

        return endpoints;
    }

    private static async Task<IResult> GetStatsAsync(
        AppDbContext db,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var last24h = now.AddHours(-24);

        var totalUsers = await db.Users.CountAsync(ct);
        var confirmedUsers = await db.Users.CountAsync(u => u.EmailConfirmed, ct);
        var lockedUsers = await db.Users.CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > now, ct);
        var mfaUsers = await db.Users.CountAsync(u => u.TwoFactorEnabled, ct);

        var activeSessions = await db.UserSessions.CountAsync(s => s.RevokedAt == null && s.ExpiresAt > now, ct);
        var failedLogins = await db.SecurityEvents.CountAsync(e =>
            (e.EventType == SecurityEventTypes.LoginFailed || e.EventType == SecurityEventTypes.LockedOut)
            && e.Timestamp >= last24h, ct);

        var mfaPercentage = totalUsers > 0 ? Math.Round((double)mfaUsers / totalUsers * 100, 1) : 0;

        return TypedResults.Ok(new AdminStatsResponse(
            TotalUsers: totalUsers,
            ConfirmedUsers: confirmedUsers,
            LockedUsers: lockedUsers,
            TwoFactorPercentage: mfaPercentage,
            ActiveSessionsCount: activeSessions,
            FailedLoginsLast24Hours: failedLogins));
    }

    private static async Task<IResult> GetUsersAsync(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] string? status,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        AppDbContext db,
        UserManager<AppUser> userManager,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is <= 0 or > 100 ? 20 : pageSize;
        var now = timeProvider.GetUtcNow();

        var query = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(u => EF.Functions.ILike(u.Email!, $"%{s}%") || EF.Functions.ILike(u.DisplayName, $"%{s}%"));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (string.Equals(status, "locked", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(u => u.LockoutEnd != null && u.LockoutEnd > now);
            }
            else if (string.Equals(status, "unconfirmed", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(u => !u.EmailConfirmed);
            }
        }

        var totalCount = await query.CountAsync(ct);
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var userResponses = new List<AdminUserResponse>(users.Count);
        foreach (var u in users)
        {
            var roles = await userManager.GetRolesAsync(u);
            if (!string.IsNullOrWhiteSpace(role) && !roles.Contains(role, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var isLocked = u.LockoutEnd.HasValue && u.LockoutEnd.Value > now;
            userResponses.Add(new AdminUserResponse(
                u.Id,
                u.Email!,
                u.DisplayName,
                u.EmailConfirmed,
                u.TwoFactorEnabled,
                isLocked,
                u.LockoutEnd,
                u.CreatedAt,
                u.LastLoginAt,
                roles.ToList()));
        }

        return TypedResults.Ok(new PagedResult<AdminUserResponse>(userResponses, totalCount, page, pageSize));
    }

    private static async Task<IResult> GetUserByIdAsync(
        Guid id,
        UserManager<AppUser> userManager,
        TimeProvider timeProvider,
        HttpContext context)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status404NotFound,
                AuthErrorCodes.UserNotFound,
                "User not found",
                "The target user account does not exist.",
                context.Request.Path);
        }

        var roles = await userManager.GetRolesAsync(user);
        var isLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > timeProvider.GetUtcNow();

        return TypedResults.Ok(new AdminUserResponse(
            user.Id,
            user.Email!,
            user.DisplayName,
            user.EmailConfirmed,
            user.TwoFactorEnabled,
            isLocked,
            user.LockoutEnd,
            user.CreatedAt,
            user.LastLoginAt,
            roles.ToList()));
    }

    private static async Task<IResult> LockUserAsync(
        Guid id,
        ClaimsPrincipal caller,
        UserManager<AppUser> userManager,
        ISessionService sessionService,
        ISecurityAuditor auditor,
        TimeProvider timeProvider,
        HttpContext context,
        CancellationToken ct)
    {
        var sub = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.FindFirstValue("sub");
        if (Guid.TryParse(sub, out var callerId) && callerId == id)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status409Conflict,
                AuthErrorCodes.SelfModificationForbidden,
                "Action not permitted",
                "You cannot lock your own account.",
                context.Request.Path);
        }

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status404NotFound,
                AuthErrorCodes.UserNotFound,
                "User not found",
                "The target user account does not exist.",
                context.Request.Path);
        }

        var now = timeProvider.GetUtcNow();
        await userManager.SetLockoutEndDateAsync(user, now.AddDays(365));

        // Revoke all sessions of locked user
        await sessionService.RevokeAllUserSessionsAsync(user.Id, "admin_locked", null, ct);

        await auditor.AuditAsync(
            SecurityEventTypes.UserLocked,
            callerId,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            new { targetUserId = id },
            ct);

        return TypedResults.Ok(new { status = "user_locked" });
    }

    private static async Task<IResult> UnlockUserAsync(
        Guid id,
        ClaimsPrincipal caller,
        UserManager<AppUser> userManager,
        ISecurityAuditor auditor,
        HttpContext context,
        CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status404NotFound,
                AuthErrorCodes.UserNotFound,
                "User not found",
                "The target user account does not exist.",
                context.Request.Path);
        }

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);

        var sub = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.FindFirstValue("sub");
        var callerId = Guid.TryParse(sub, out var cid) ? cid : (Guid?)null;

        await auditor.AuditAsync(
            SecurityEventTypes.UserUnlocked,
            callerId,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            new { targetUserId = id },
            ct);

        return TypedResults.Ok(new { status = "user_unlocked" });
    }

    private static async Task<IResult> UpdateUserRolesAsync(
        Guid id,
        [FromBody] UpdateUserRolesRequest request,
        ClaimsPrincipal caller,
        UserManager<AppUser> userManager,
        RoleManager<AppRole> roleManager,
        ISessionService sessionService,
        ISecurityAuditor auditor,
        HttpContext context,
        CancellationToken ct)
    {
        var sub = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.FindFirstValue("sub");
        if (Guid.TryParse(sub, out var callerId) && callerId == id)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status409Conflict,
                AuthErrorCodes.SelfModificationForbidden,
                "Action not permitted",
                "You cannot modify your own administrative roles.",
                context.Request.Path);
        }

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status404NotFound,
                AuthErrorCodes.UserNotFound,
                "User not found",
                "The target user account does not exist.",
                context.Request.Path);
        }

        // Validate requested roles
        foreach (var r in request.Roles)
        {
            if (!await roleManager.RoleExistsAsync(r))
            {
                return ApiProblemDetails.CreateResult(
                    StatusCodes.Status400BadRequest,
                    "role.invalid",
                    "Invalid role",
                    $"Role '{r}' does not exist.",
                    context.Request.Path);
            }
        }

        // Last-Admin rule: If removing Admin role from target, ensure at least one other Admin exists
        var currentRoles = await userManager.GetRolesAsync(user);
        var isCurrentlyAdmin = currentRoles.Contains(Roles.Admin, StringComparer.OrdinalIgnoreCase);
        var willBeAdmin = request.Roles.Contains(Roles.Admin, StringComparer.OrdinalIgnoreCase);

        if (isCurrentlyAdmin && !willBeAdmin)
        {
            var adminUsers = await userManager.GetUsersInRoleAsync(Roles.Admin);
            if (adminUsers.Count <= 1)
            {
                return ApiProblemDetails.CreateResult(
                    StatusCodes.Status409Conflict,
                    AuthErrorCodes.LastAdmin,
                    "Last admin protection",
                    "Cannot demote the last remaining system Administrator.",
                    context.Request.Path);
            }
        }

        await userManager.RemoveFromRolesAsync(user, currentRoles);
        await userManager.AddToRolesAsync(user, request.Roles);

        // Role modification revokes target user's active sessions immediately
        await sessionService.RevokeAllUserSessionsAsync(user.Id, "roles_changed", null, ct);

        await auditor.AuditAsync(
            SecurityEventTypes.RoleChanged,
            callerId,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            new { targetUserId = id, oldRoles = currentRoles, newRoles = request.Roles },
            ct);

        return TypedResults.Ok(new { status = "roles_updated" });
    }

    private static async Task<IResult> RevokeUserSessionsAsync(
        Guid id,
        ClaimsPrincipal caller,
        UserManager<AppUser> userManager,
        ISessionService sessionService,
        ISecurityAuditor auditor,
        HttpContext context,
        CancellationToken ct)
    {
        var sub = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.FindFirstValue("sub");
        if (Guid.TryParse(sub, out var callerId) && callerId == id)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status409Conflict,
                AuthErrorCodes.SelfModificationForbidden,
                "Action not permitted",
                "Use the standard logout endpoint to end your own session.",
                context.Request.Path);
        }

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status404NotFound,
                AuthErrorCodes.UserNotFound,
                "User not found",
                "The target user account does not exist.",
                context.Request.Path);
        }

        await sessionService.RevokeAllUserSessionsAsync(user.Id, "admin_revocation", null, ct);

        await auditor.AuditAsync(
            SecurityEventTypes.SessionRevoked,
            callerId,
            context.Connection.RemoteIpAddress?.ToString(),
            context.Request.Headers.UserAgent.ToString(),
            new { targetUserId = id, revokedByAdmin = true },
            ct);

        return TypedResults.Ok(new { status = "sessions_revoked" });
    }

    private static async Task<IResult> ResendUserConfirmationAsync(
        Guid id,
        UserManager<AppUser> userManager,
        IEmailQueue emailQueue,
        IConfiguration config,
        HttpContext context,
        CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status404NotFound,
                AuthErrorCodes.UserNotFound,
                "User not found",
                "The target user account does not exist.",
                context.Request.Path);
        }

        if (!user.EmailConfirmed)
        {
            var webUrl = config["Web:SiteUrl"] ?? "http://localhost:3000";
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var email = EmailTemplates.ConfirmEmail(webUrl, user.Id, token, user.Email!);
            await emailQueue.QueueEmailAsync(email, ct);
        }

        return TypedResults.Ok(new { status = "confirmation_resent" });
    }

    private static async Task<IResult> GetRolesAsync(
        RoleManager<AppRole> roleManager,
        CancellationToken ct)
    {
        var roles = await roleManager.Roles.AsNoTracking().ToListAsync(ct);
        var result = new List<RoleResponse>(roles.Count);

        foreach (var r in roles)
        {
            var claims = await roleManager.GetClaimsAsync(r);
            var permissions = claims
                .Where(c => c.Type == "permission")
                .Select(c => c.Value)
                .ToList();

            result.Add(new RoleResponse(r.Name!, r.Description, permissions));
        }

        return TypedResults.Ok(result);
    }

    private static async Task<IResult> GetAuditEventsAsync(
        [FromQuery] string? eventType,
        [FromQuery] Guid? userId,
        [FromQuery] int page,
        [FromQuery] int pageSize,
        AppDbContext db,
        CancellationToken ct)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize is <= 0 or > 100 ? 50 : pageSize;

        var query = db.SecurityEvents.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(e => e.EventType == eventType);
        }

        if (userId.HasValue)
        {
            query = query.Where(e => e.UserId == userId.Value);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(e => e.User)
            .Select(e => new SecurityEventResponse(
                e.Id,
                e.EventType,
                e.UserId,
                e.User != null ? e.User.Email : null,
                e.IpAddress,
                e.UserAgent,
                e.Timestamp,
                e.DetailsJson))
            .ToListAsync(ct);

        return TypedResults.Ok(new PagedResult<SecurityEventResponse>(items, total, page, pageSize));
    }

    private static async Task<IResult> GetNotesForModerationAsync(
        AppDbContext db,
        CancellationToken ct)
    {
        var notes = await db.Notes
            .AsNoTracking()
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NoteResponse(n.Id, n.OwnerId, n.Title, n.Content, n.CreatedAt, n.UpdatedAt))
            .ToListAsync(ct);

        return TypedResults.Ok(notes);
    }
}
