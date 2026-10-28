namespace Identity.Api.Common.Constants;

/// <summary>
/// Predefined application roles.
/// Roles are strictly bundles of permissions and are never referenced directly in endpoint authorization checks.
/// </summary>
public static class Roles
{
    public const string User = "User";
    public const string Support = "Support";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [User, Support, Admin];

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> RolePermissions =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [User] = [Permissions.ProfileManage, Permissions.NotesManageOwn],
            [Support] =
            [
                Permissions.ProfileManage,
                Permissions.NotesManageOwn,
                Permissions.UsersRead,
                Permissions.AuditRead
            ],
            [Admin] =
            [
                Permissions.ProfileManage,
                Permissions.NotesManageOwn,
                Permissions.UsersRead,
                Permissions.AuditRead,
                Permissions.UsersManage,
                Permissions.NotesModerate
            ]
        };
}
