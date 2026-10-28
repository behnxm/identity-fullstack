using System.Security.Claims;
using Identity.Api.Common.Options;
using Identity.Api.Persistence.Entities;
using Identity.Api.Security.Keys;
using Identity.Api.Security.Tokens;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shouldly;
using Xunit;

namespace Identity.UnitTests.Security;

public class TokenServiceTests
{
    private readonly IKeyManagementService _keyService;
    private readonly IOptions<JwtOptions> _jwtOptions;
    private readonly TokenService _sut;

    public TokenServiceTests()
    {
        _jwtOptions = Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Issuer = "https://identity.local",
            Audience = "https://identity.local/api",
            KeyId = "test-key-1",
            AccessTokenLifetime = TimeSpan.FromMinutes(10),
            ClockSkew = TimeSpan.FromSeconds(30)
        });

        var hostEnv = new TestHostEnvironment { EnvironmentName = "Development" };
        _keyService = new KeyManagementService(_jwtOptions, hostEnv, NullLogger<KeyManagementService>.Instance);
        _sut = new TokenService(_keyService, _jwtOptions);
    }

    [Fact]
    public async Task GenerateAccessToken_ProducesValidEs256Token_WithAllRequiredClaims()
    {
        // Arrange
        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            UserName = "alice@example.com",
            Email = "alice@example.com",
            DisplayName = "Alice Smith",
            EmailConfirmed = true
        };

        var session = new UserSession
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            RememberMe = true,
            MfaVerifiedAt = DateTimeOffset.UtcNow
        };

        var roles = new[] { "Admin", "User" };
        var permissions = new[] { "users.read", "profile.manage" };
        var issuedAt = DateTimeOffset.UtcNow;

        // Act
        var tokenString = _sut.GenerateAccessToken(user, session, roles, permissions, issuedAt);

        // Assert
        tokenString.ShouldNotBeNullOrWhiteSpace();

        // 1. Token size check: well under 1.5 KB
        tokenString.Length.ShouldBeLessThan(1500);

        // 2. Validate token format and header with JsonWebTokenHandler
        var handler = new JsonWebTokenHandler();
        var jwt = handler.ReadJsonWebToken(tokenString);

        jwt.Alg.ShouldBe(SecurityAlgorithms.EcdsaSha256); // ES256
        jwt.Typ.ShouldBe("at+jwt"); // RFC 9068 required header typ
        jwt.Kid.ShouldBe("test-key-1");

        // 3. Claims verification
        jwt.Issuer.ShouldBe("https://identity.local");
        jwt.Audiences.ShouldContain("https://identity.local/api");
        jwt.Subject.ShouldBe(user.Id.ToString());
        jwt.Id.ShouldNotBeNullOrWhiteSpace(); // jti claim
        jwt.GetClaim("sid").Value.ShouldBe(session.Id.ToString());
        jwt.GetClaim("email").Value.ShouldBe("alice@example.com");
        jwt.GetClaim("email_verified").Value.ShouldBe("true");
        jwt.GetClaim("name").Value.ShouldBe("Alice Smith");

        // AMR claim contains pwd and otp when session has MfaVerifiedAt
        var amrValues = jwt.Claims.Where(c => c.Type == "amr").Select(c => c.Value).ToList();
        amrValues.ShouldContain("pwd");
        amrValues.ShouldContain("otp");

        // Roles and permissions
        var roleValues = jwt.Claims.Where(c => c.Type == "role").Select(c => c.Value).ToList();
        roleValues.ShouldContain("Admin");
        roleValues.ShouldContain("User");

        var permValues = jwt.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToList();
        permValues.ShouldContain("users.read");
        permValues.ShouldContain("profile.manage");

        // 4. Cryptographic signature verification using active validation key
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://identity.local",
            ValidateAudience = true,
            ValidAudience = "https://identity.local/api",
            ValidateLifetime = true,
            ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
            ValidTypes = ["at+jwt"],
            IssuerSigningKey = _keyService.GetCurrentSigningKey()
        };

        var validationResult = await handler.ValidateTokenAsync(tokenString, validationParameters);
        validationResult.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void GenerateAccessToken_WithoutMfa_AmrContainsOnlyPwd()
    {
        // Arrange
        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            UserName = "bob@example.com",
            DisplayName = "Bob Jones"
        };
        var session = new UserSession
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            MfaVerifiedAt = null
        };

        // Act
        var tokenString = _sut.GenerateAccessToken(user, session, ["User"], ["profile.manage"], DateTimeOffset.UtcNow);

        // Assert
        var handler = new JsonWebTokenHandler();
        var jwt = handler.ReadJsonWebToken(tokenString);
        var amrValues = jwt.Claims.Where(c => c.Type == "amr").Select(c => c.Value).ToList();

        amrValues.ShouldContain("pwd");
        amrValues.ShouldNotContain("otp");
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
