namespace Identity.Api.Common.Options;

/// <summary>
/// Options for database seeding of roles, permissions, and demo users.
/// </summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public bool Enabled { get; set; } = true;
    public bool DemoUsers { get; set; } = true;
    public bool AllowDemoInProduction { get; set; } = false;
}
