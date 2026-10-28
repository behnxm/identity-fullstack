using System.ComponentModel.DataAnnotations;

namespace Identity.Api.Admin;

/// <summary>System security and user statistics for admin panel overview.</summary>
public sealed record AdminStatsResponse(
    int TotalUsers,
    int ConfirmedUsers,
    int LockedUsers,
    double TwoFactorPercentage,
    int ActiveSessionsCount,
    int FailedLoginsLast24Hours);

/// <summary>Admin view of a user profile.</summary>
public sealed record AdminUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    bool EmailConfirmed,
    bool TwoFactorEnabled,
    bool IsLockedOut,
    DateTimeOffset? LockoutEnd,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    IReadOnlyList<string> Roles);

/// <summary>Request payload for updating user roles.</summary>
public sealed record UpdateUserRolesRequest
{
    [Required]
    public required IReadOnlyList<string> Roles { get; init; }
}

/// <summary>Role metadata and associated permissions.</summary>
public sealed record RoleResponse(
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions);

/// <summary>Security audit log entry item.</summary>
public sealed record SecurityEventResponse(
    Guid Id,
    string EventType,
    Guid? UserId,
    string? UserEmail,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset Timestamp,
    string? DetailsJson);

/// <summary>Paginated response envelope.</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
