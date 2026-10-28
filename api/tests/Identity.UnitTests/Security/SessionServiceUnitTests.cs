using Identity.Api.Security.Sessions;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace Identity.UnitTests.Security;

public class SessionServiceUnitTests
{
    [Fact]
    public void GenerateOpaqueToken_Produces256BitBase64UrlToken()
    {
        var token1 = SessionService.GenerateOpaqueToken();
        var token2 = SessionService.GenerateOpaqueToken();

        token1.ShouldNotBeNullOrWhiteSpace();
        token2.ShouldNotBeNullOrWhiteSpace();
        token1.ShouldNotBe(token2);

        // 32 bytes (256 bits) in Base64Url is 43 characters without padding
        var decodedBytes = Base64UrlEncoder.DecodeBytes(token1);
        decodedBytes.Length.ShouldBe(32);
    }

    [Fact]
    public void HashToken_Produces64CharHexSha256Digest()
    {
        var raw = "test-token-123456";
        var hash = SessionService.HashToken(raw);

        hash.Length.ShouldBe(64);
        hash.ShouldMatch("^[a-f0-9]{64}$");

        // Deterministic
        SessionService.HashToken(raw).ShouldBe(hash);
    }
}
