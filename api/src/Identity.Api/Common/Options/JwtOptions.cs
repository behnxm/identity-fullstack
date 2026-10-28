namespace Identity.Api.Common.Options;

/// <summary>
/// Configuration options for JSON Web Token generation, validation, and key resolution.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "https://identity.local";
    public string Audience { get; set; } = "https://identity.local/api";
    public string KeyId { get; set; } = "local-dev-key-1";
    public string? SigningKeyPath { get; set; }
    public string? PrivateKeyPem { get; set; }
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Previous public keys for seamless zero-downtime key rotation.
    /// Key is key ID (kid), Value is public key PEM or JWK JSON string.
    /// </summary>
    public Dictionary<string, string> PreviousPublicKeys { get; set; } = new(StringComparer.Ordinal);
}
