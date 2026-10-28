using System.Security.Claims;
using Identity.Api.Common.Options;
using Identity.Api.Persistence.Entities;
using Identity.Api.Security.Keys;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Api.Security.Tokens;

/// <summary>
/// Issues ES256-signed JWT access tokens conforming to RFC 9068 (JWT Profile for OAuth 2.0 Access Tokens).
/// </summary>
public sealed class TokenService(
    IKeyManagementService keyManagementService,
    IOptions<JwtOptions> jwtOptions)
    : ITokenService
{
    private readonly JsonWebTokenHandler _tokenHandler = new();
    private readonly JwtOptions _options = jwtOptions.Value;

    public string GenerateAccessToken(
        AppUser user,
        UserSession session,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> permissions,
        DateTimeOffset issuedAt)
    {
        var signingKey = keyManagementService.GetCurrentSigningKey();
        var keyId = keyManagementService.GetCurrentKeyId();
        var expiresAt = issuedAt.Add(_options.AccessTokenLifetime);

        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
            [JwtRegisteredClaimNames.Jti] = Guid.CreateVersion7().ToString(),
            ["sid"] = session.Id.ToString(),
            [JwtRegisteredClaimNames.Email] = user.Email ?? string.Empty,
            ["email_verified"] = user.EmailConfirmed,
            ["name"] = user.DisplayName,
            ["amr"] = session.MfaVerifiedAt.HasValue
                ? new[] { "pwd", "otp" }
                : new[] { "pwd" }
        };

        if (roles.Count > 0)
        {
            claims["role"] = roles.ToArray();
        }

        if (permissions.Count > 0)
        {
            claims["permission"] = permissions.ToArray();
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expiresAt.UtcDateTime,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.EcdsaSha256),
            TokenType = "at+jwt", // Header typ: at+jwt
            Claims = claims
        };

        return _tokenHandler.CreateToken(descriptor);
    }
}
