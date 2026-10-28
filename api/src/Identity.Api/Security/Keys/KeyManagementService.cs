using System.Security.Cryptography;
using Identity.Api.Common.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Identity.Api.Security.Keys;

/// <summary>
/// Implements ES256 (ECDSA P-256) key management, key rotation support, and JWKS publishing.
/// </summary>
public sealed class KeyManagementService : IKeyManagementService
{
    private readonly ECDsaSecurityKey _activeSigningKey;
    private readonly string _activeKeyId;
    private readonly Dictionary<string, SecurityKey> _validationKeys = new(StringComparer.Ordinal);
    private readonly JsonWebKeySet _jwks;

    public KeyManagementService(
        IOptions<JwtOptions> jwtOptions,
        IWebHostEnvironment environment,
        ILogger<KeyManagementService> logger)
    {
        var options = jwtOptions.Value;
        _activeKeyId = string.IsNullOrWhiteSpace(options.KeyId) ? "key-1" : options.KeyId;

        var ecdsa = LoadOrGenerateActiveKey(options, environment, logger);
        _activeSigningKey = new ECDsaSecurityKey(ecdsa)
        {
            KeyId = _activeKeyId
        };

        _validationKeys[_activeKeyId] = _activeSigningKey;

        // Load previous keys for rotation
        if (options.PreviousPublicKeys is { Count: > 0 })
        {
            foreach (var (kid, pem) in options.PreviousPublicKeys)
            {
                try
                {
                    var prevEcdsa = ECDsa.Create();
                    prevEcdsa.ImportFromPem(pem);
                    var prevKey = new ECDsaSecurityKey(prevEcdsa) { KeyId = kid };
                    _validationKeys[kid] = prevKey;
                    logger.LogInformation("Loaded previous public key with kid: {Kid}", kid);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to load previous key for kid: {Kid}", kid);
                }
            }
        }

        // Build JWKS
        _jwks = BuildJwks();
    }

    public ECDsaSecurityKey GetCurrentSigningKey() => _activeSigningKey;

    public string GetCurrentKeyId() => _activeKeyId;

    public IReadOnlyList<SecurityKey> GetAllValidationKeys() => _validationKeys.Values.ToList();

    public SecurityKey? FindValidationKey(string kid) =>
        _validationKeys.TryGetValue(kid, out var key) ? key : null;

    public JsonWebKeySet GetJwks() => _jwks;

    private static ECDsa LoadOrGenerateActiveKey(
        JwtOptions options,
        IWebHostEnvironment environment,
        ILogger logger)
    {
        // 1. Try configured file path
        if (!string.IsNullOrWhiteSpace(options.SigningKeyPath) && File.Exists(options.SigningKeyPath))
        {
            logger.LogInformation("Loading ES256 signing key from file: {Path}", options.SigningKeyPath);
            var pem = File.ReadAllText(options.SigningKeyPath);
            var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(pem);
            return ecdsa;
        }

        // 2. Try configured inline PEM
        if (!string.IsNullOrWhiteSpace(options.PrivateKeyPem))
        {
            logger.LogInformation("Loading ES256 signing key from configured PEM string");
            var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(options.PrivateKeyPem);
            return ecdsa;
        }

        // 3. In Development only: look in secrets/ or auto-generate
        if (environment.IsDevelopment())
        {
            var secretsDir = Path.Combine(Directory.GetCurrentDirectory(), "secrets");
            var devKeyFile = Path.Combine(secretsDir, "signing-key.pem");

            if (File.Exists(devKeyFile))
            {
                logger.LogInformation("Loading development ES256 signing key from {Path}", devKeyFile);
                var pem = File.ReadAllText(devKeyFile);
                var ecdsa = ECDsa.Create();
                ecdsa.ImportFromPem(pem);
                return ecdsa;
            }

            logger.LogWarning("Auto-generating development ES256 signing key into {Path}", devKeyFile);
            Directory.CreateDirectory(secretsDir);
            var newEcdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var exportedPem = newEcdsa.ExportECPrivateKeyPem();
            File.WriteAllText(devKeyFile, exportedPem);
            return newEcdsa;
        }

        // In Staging/Production: Refuse to start without configured key
        throw new InvalidOperationException(
            "ES256 signing key must be configured in non-Development environments via Jwt:SigningKeyPath or Jwt:PrivateKeyPem.");
    }

    private JsonWebKeySet BuildJwks()
    {
        var jwks = new JsonWebKeySet();

        foreach (var (kid, secKey) in _validationKeys)
        {
            if (secKey is ECDsaSecurityKey ecKey)
            {
                var parameters = ecKey.ECDsa.ExportParameters(false);
                var jwk = new JsonWebKey
                {
                    Kty = JsonWebAlgorithmsKeyTypes.EllipticCurve,
                    Use = "sig",
                    Kid = kid,
                    Alg = SecurityAlgorithms.EcdsaSha256,
                    Crv = JsonWebKeyECTypes.P256,
                    X = Base64UrlEncoder.Encode(parameters.Q.X!),
                    Y = Base64UrlEncoder.Encode(parameters.Q.Y!)
                };
                jwks.Keys.Add(jwk);
            }
        }

        return jwks;
    }
}
