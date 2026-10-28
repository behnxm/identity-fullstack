namespace Identity.Api.Common.Constants;

/// <summary>
/// Central string constants for application permissions.
/// These represent granular capabilities and are the single source of truth for authorization.
/// </summary>
public static class Permissions
{
    public const string ProfileManage = "profile.manage";
    public const string NotesManageOwn = "notes.manage.own";
    public const string UsersRead = "users.read";
    public const string AuditRead = "audit.read";
    public const string UsersManage = "users.manage";
    public const string NotesModerate = "notes.moderate";

    public static readonly IReadOnlyList<string> All =
    [
        ProfileManage,
        NotesManageOwn,
        UsersRead,
        AuditRead,
        UsersManage,
        NotesModerate
    ];
}
