using System.Security.Cryptography;
using Identity.Api.Common.Options;
using Identity.Api.Security.Keys;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace Identity.UnitTests.Security;

public class KeyManagementTests
{
    [Fact]
    public void GetJwks_ReturnsValidEcP256PublicKeys()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            KeyId = "active-key",
            Issuer = "https://identity.local"
        });

        var env = new TestHostEnvironment { EnvironmentName = "Development" };
        var sut = new KeyManagementService(options, env, NullLogger<KeyManagementService>.Instance);

        var jwks = sut.GetJwks();

        jwks.ShouldNotBeNull();
        jwks.Keys.Count.ShouldBeGreaterThanOrEqualTo(1);

        var activeKey = jwks.Keys.FirstOrDefault(k => k.Kid == "active-key");
        activeKey.ShouldNotBeNull();
        activeKey.Kty.ShouldBe("EC");
        activeKey.Crv.ShouldBe("P-256");
        activeKey.Use.ShouldBe("sig");
        activeKey.Alg.ShouldBe("ES256");
        activeKey.X.ShouldNotBeNullOrWhiteSpace();
        activeKey.Y.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void KeyRotation_PreviousKeys_AreRetrievableForVerification()
    {
        // Generate previous key PEM
        using var prevEcdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var prevPem = prevEcdsa.ExportSubjectPublicKeyInfoPem();

        var options = Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            KeyId = "active-key-v2",
            PreviousPublicKeys = new Dictionary<string, string>
            {
                ["active-key-v1"] = prevPem
            }
        });

        var env = new TestHostEnvironment { EnvironmentName = "Development" };
        var sut = new KeyManagementService(options, env, NullLogger<KeyManagementService>.Instance);

        var active = sut.FindValidationKey("active-key-v2");
        var previous = sut.FindValidationKey("active-key-v1");
        var unknown = sut.FindValidationKey("unknown-key");

        active.ShouldNotBeNull();
        previous.ShouldNotBeNull();
        unknown.ShouldBeNull();
    }

    [Fact]
    public void Production_WithoutConfiguredKey_ThrowsInvalidOperationException()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            SigningKeyPath = null,
            PrivateKeyPem = null
        });

        var env = new TestHostEnvironment { EnvironmentName = "Production" };

        Should.Throw<InvalidOperationException>(() =>
            new KeyManagementService(options, env, NullLogger<KeyManagementService>.Instance));
    }

    private sealed class TestHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Identity.Api";
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
