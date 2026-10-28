namespace Identity.Api.Common.Constants;

/// <summary>
/// Constants for ASP.NET Core authorization policy names.
/// All policy-based authorization across endpoints and evaluation APIs references these values.
/// </summary>
public static class Policies
{
    public const string PanelUser = "panel.user";
    public const string PanelAdmin = "panel.admin";

    public const string UsersRead = "users.read";
    public const string AuditRead = "audit.read";
    public const string UsersManage = "users.manage";
    public const string NotesModerate = "notes.moderate";
    public const string NotesManageOwn = "notes.manage.own";

    public const string NoteAccess = "note.access";

    public static readonly IReadOnlyList<string> EvaluatablePolicies =
    [
        PanelUser,
        PanelAdmin,
        UsersRead,
        AuditRead,
        UsersManage,
        NotesModerate,
        NotesManageOwn
    ];
}
