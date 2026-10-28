namespace Identity.Api.Common.Options;

/// <summary>
/// Options governing user sessions, sliding idle timeouts, refresh token rotation, and grace windows.
/// </summary>
public sealed class UserSessionOptions
{
    public const string SectionName = "Session";

    public TimeSpan RememberMeAbsoluteLifetime { get; set; } = TimeSpan.FromDays(30);
    public TimeSpan DefaultAbsoluteLifetime { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan RememberMeIdleLifetime { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan DefaultIdleLifetime { get; set; } = TimeSpan.FromHours(8);
    public int MaxActiveSessionsPerUser { get; set; } = 10;
    public TimeSpan RefreshGraceWindow { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan HybridCacheTtl { get; set; } = TimeSpan.FromSeconds(15);
}
