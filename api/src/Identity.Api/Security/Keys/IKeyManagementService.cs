using Microsoft.IdentityModel.Tokens;

namespace Identity.Api.Security.Keys;

/// <summary>
/// Manages ES256 (ECDSA P-256) cryptographic keys for token signing, verification, and JWKS publication.
/// </summary>
public interface IKeyManagementService
{
    /// <summary>
    /// Gets the current active signing key with its Key ID.
    /// </summary>
    ECDsaSecurityKey GetCurrentSigningKey();

    /// <summary>
    /// Gets the Key ID (kid) of the current active signing key.
    /// </summary>
    string GetCurrentKeyId();

    /// <summary>
    /// Gets all valid keys for token verification (active key + previous keys for seamless rotation).
    /// </summary>
    IReadOnlyList<SecurityKey> GetAllValidationKeys();

    /// <summary>
    /// Gets a specific validation key by its Key ID.
    /// </summary>
    SecurityKey? FindValidationKey(string kid);

    /// <summary>
    /// Gets the RFC 7517 JSON Web Key Set (JWKS) containing public keys for /.well-known/jwks.json.
    /// </summary>
    JsonWebKeySet GetJwks();
}
