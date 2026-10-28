using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Identity.Api.Common.Options;
using Identity.Api.Persistence.Entities;
using Identity.Api.Security.Keys;
using Identity.Api.Security.Tokens;
using Identity.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace Identity.IntegrationTests.Security;

[Collection("Integration")]
public class JwtAttackTests(CustomWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task TamperedSignature_IsRejectedWith401()
    {
        var validToken = await factory.IssueAccessTokenAsync(Guid.CreateVersion7(), "user@demo.test", "User", false);
        var parts = validToken.Split('.');
        // Tamper payload byte
        var payloadJson = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(parts[1]));
        var tamperedPayload = payloadJson.Replace("user@demo.test", "hacker@demo.test");
        var tamperedToken = $"{parts[0]}.{Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(tamperedPayload))}.{parts[2]}";

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tamperedToken);
        var res = await _client.SendAsync(req);

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AlgNone_IsRejectedWith401()
    {
        var header = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"at+jwt\"}"));
        var payload = Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes("{\"sub\":\"123\",\"iss\":\"https://identity.local\",\"aud\":\"https://identity.local/api\",\"sid\":\"" + Guid.NewGuid() + "\"}"));
        var unsignedToken = $"{header}.{payload}.";

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", unsignedToken);
        var res = await _client.SendAsync(req);

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Hs256AlgorithmConfusion_IsRejectedWith401()
    {
        // Attack: Attacker signs an HS256 token using the public EC key bytes as HMAC secret
        using var scope = factory.Services.CreateScope();
        var keyService = scope.ServiceProvider.GetRequiredService<IKeyManagementService>();
        var ecKey = keyService.GetCurrentSigningKey();
        var pubParams = ecKey.ECDsa.ExportSubjectPublicKeyInfo();

        var handler = new JsonWebTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "https://identity.local",
            Audience = "https://identity.local/api",
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(pubParams), SecurityAlgorithms.HmacSha256),
            TokenType = "at+jwt",
            Claims = new Dictionary<string, object>
            {
                ["sub"] = Guid.NewGuid().ToString(),
                ["sid"] = Guid.NewGuid().ToString()
            }
        };

        var confusedToken = handler.CreateToken(descriptor);

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", confusedToken);
        var res = await _client.SendAsync(req);

        // Rejected: ValidAlgorithms = [ES256] only!
        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WrongTypHeader_IsRejectedWith401()
    {
        using var scope = factory.Services.CreateScope();
        var keyService = scope.ServiceProvider.GetRequiredService<IKeyManagementService>();
        var jwtOptions = scope.ServiceProvider.GetRequiredService<IOptions<JwtOptions>>().Value;

        var handler = new JsonWebTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwtOptions.Issuer,
            Audience = jwtOptions.Audience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(keyService.GetCurrentSigningKey(), SecurityAlgorithms.EcdsaSha256),
            TokenType = "JWT", // Should be "at+jwt"
            Claims = new Dictionary<string, object>
            {
                ["sub"] = Guid.NewGuid().ToString(),
                ["sid"] = Guid.NewGuid().ToString()
            }
        };

        var invalidTypToken = handler.CreateToken(descriptor);

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", invalidTypToken);
        var res = await _client.SendAsync(req);

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnknownKidHeader_IsRejectedWith401()
    {
        // Sign with an ephemeral unknown EC key
        using var rogueEcdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var rogueKey = new ECDsaSecurityKey(rogueEcdsa) { KeyId = "rogue-unknown-kid-999" };

        var handler = new JsonWebTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "https://identity.local",
            Audience = "https://identity.local/api",
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(rogueKey, SecurityAlgorithms.EcdsaSha256),
            TokenType = "at+jwt",
            Claims = new Dictionary<string, object>
            {
                ["sub"] = Guid.NewGuid().ToString(),
                ["sid"] = Guid.NewGuid().ToString()
            }
        };

        var rogueToken = handler.CreateToken(descriptor);

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rogueToken);
        var res = await _client.SendAsync(req);

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task WrongIssuerOrAudience_IsRejectedWith401()
    {
        using var scope = factory.Services.CreateScope();
        var keyService = scope.ServiceProvider.GetRequiredService<IKeyManagementService>();

        var handler = new JsonWebTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "https://evil.issuer.com",
            Audience = "https://identity.local/api",
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(keyService.GetCurrentSigningKey(), SecurityAlgorithms.EcdsaSha256),
            TokenType = "at+jwt",
            Claims = new Dictionary<string, object>
            {
                ["sub"] = Guid.NewGuid().ToString(),
                ["sid"] = Guid.NewGuid().ToString()
            }
        };

        var rogueToken = handler.CreateToken(descriptor);

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", rogueToken);
        var res = await _client.SendAsync(req);

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MissingSidClaim_IsRejectedWith401()
    {
        using var scope = factory.Services.CreateScope();
        var keyService = scope.ServiceProvider.GetRequiredService<IKeyManagementService>();
        var jwtOptions = scope.ServiceProvider.GetRequiredService<IOptions<JwtOptions>>().Value;

        var handler = new JsonWebTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwtOptions.Issuer,
            Audience = jwtOptions.Audience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(keyService.GetCurrentSigningKey(), SecurityAlgorithms.EcdsaSha256),
            TokenType = "at+jwt",
            Claims = new Dictionary<string, object>
            {
                ["sub"] = Guid.NewGuid().ToString()
                // MISSING "sid" claim!
            }
        };

        var noSidToken = handler.CreateToken(descriptor);

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/me");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", noSidToken);
        var res = await _client.SendAsync(req);

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
