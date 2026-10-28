using Identity.Api.Common.Options;
using Identity.Api.Persistence.Entities;
using Identity.Api.Security.Password;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Identity.UnitTests.Security;

public class PasswordValidatorTests
{
    private readonly CustomPasswordValidator _sut;

    public PasswordValidatorTests()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new SecurityPolicyOptions
        {
            BreachedPasswordCheckEnabled = false
        });

        _sut = new CustomPasswordValidator(
            new StubBreachedPasswordChecker(),
            options,
            NullLogger<CustomPasswordValidator>.Instance);
    }

    [Theory]
    [InlineData("correct horse battery staple")] // All lowercase passphrase (NIST recommendation)
    [InlineData("my-very-secure-password-123")]
    [InlineData("Str0ng!P@ssw0rd#2026")]
    public async Task ValidateAsync_ValidPasswords_PassValidation(string password)
    {
        var user = new AppUser
        {
            UserName = "test@example.com",
            Email = "test@example.com",
            DisplayName = "Test User"
        };

        var result = await _sut.ValidateAsync(null!, user, password);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_PasswordUnder12Characters_FailsWithTooShort()
    {
        var user = new AppUser { DisplayName = "Alice", Email = "alice@example.com" };

        var result = await _sut.ValidateAsync(null!, user, "Short1!pass");

        result.Succeeded.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "PasswordTooShort");
    }

    [Fact]
    public async Task ValidateAsync_PasswordOver128Characters_FailsWithTooLong()
    {
        var user = new AppUser { DisplayName = "Alice", Email = "alice@example.com" };
        var longPassword = new string('a', 129);

        var result = await _sut.ValidateAsync(null!, user, longPassword);

        result.Succeeded.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "PasswordTooLong");
    }

    [Theory]
    [InlineData("password12345")]
    [InlineData("123456789012")]
    [InlineData("welcome123456")]
    [InlineData("qwertyuiop12")]
    public async Task ValidateAsync_CommonPasswordsBlocklist_FailsWithTooCommon(string commonPassword)
    {
        var user = new AppUser { DisplayName = "Alice", Email = "alice@example.com" };

        var result = await _sut.ValidateAsync(null!, user, commonPassword);

        result.Succeeded.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "PasswordTooCommon");
    }

    [Fact]
    public async Task ValidateAsync_PasswordContainingEmailPrefix_FailsWithContainsEmail()
    {
        var user = new AppUser { DisplayName = "Alice Smith", Email = "charlie99@example.com" };

        var result = await _sut.ValidateAsync(null!, user, "my-charlie99-secret");

        result.Succeeded.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "PasswordContainsEmail");
    }

    [Fact]
    public async Task ValidateAsync_PasswordContainingDisplayName_FailsWithContainsDisplayName()
    {
        var user = new AppUser { DisplayName = "Alexander", Email = "alex@example.com" };

        var result = await _sut.ValidateAsync(null!, user, "AlexanderSafeKey123");

        result.Succeeded.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Code == "PasswordContainsDisplayName");
    }
}
