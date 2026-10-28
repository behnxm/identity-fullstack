using Identity.Api.Persistence.Entities;

namespace Identity.Api.Security.Tokens;

/// <summary>
/// Service for generating signed RFC 9068-compliant JWT access tokens with ES256.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Issues a signed JWT access token with typ: at+jwt, kid, and explicit user, session, and permission claims.
    /// </summary>
    string GenerateAccessToken(
        AppUser user,
        UserSession session,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> permissions,
        DateTimeOffset issuedAt);
}
