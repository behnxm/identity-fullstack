namespace Identity.Api.Security.Password;

/// <summary>
/// Service abstraction for checking whether a password has appeared in known data breaches.
/// </summary>
public interface IBreachedPasswordChecker
{
    /// <summary>
    /// Checks whether the candidate password has been exposed in a known breach.
    /// Fails open (returns false) with warning logs if external verification service is unreachable.
    /// </summary>
    Task<bool> IsPasswordBreachedAsync(string password, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation that can be toggled via Security:BreachedPasswordCheckEnabled.
/// Fails open to prevent breaking registration or password reset flows during upstream outages.
/// </summary>
public sealed class StubBreachedPasswordChecker : IBreachedPasswordChecker
{
    public Task<bool> IsPasswordBreachedAsync(string password, CancellationToken cancellationToken = default)
    {
        // Fail open by default (or check against an in-memory sample blocklist)
        return Task.FromResult(false);
    }
}
