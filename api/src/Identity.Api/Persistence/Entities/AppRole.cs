using Microsoft.AspNetCore.Identity;

namespace Identity.Api.Persistence.Entities;

/// <summary>
/// Custom identity role entity with Guid primary keys and role description.
/// </summary>
public class AppRole : IdentityRole<Guid>
{
    public AppRole() : base() { }

    public AppRole(string roleName, string? description = null) : base(roleName)
    {
        Description = description;
    }

    public string? Description { get; set; }
}
