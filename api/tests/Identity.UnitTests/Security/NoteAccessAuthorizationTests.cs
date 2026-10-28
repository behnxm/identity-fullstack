using System.Security.Claims;
using Identity.Api.Common.Constants;
using Identity.Api.Persistence.Entities;
using Identity.Api.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Shouldly;
using Xunit;

namespace Identity.UnitTests.Security;

public class NoteAccessAuthorizationTests
{
    private readonly NoteAccessAuthorizationHandler _sut = new();

    [Theory]
    [InlineData("Read", true)]
    [InlineData("Update", true)]
    [InlineData("Delete", true)]
    public async Task Owner_HasFullAccess_ToNote(string operation, bool expectedAllowed)
    {
        var ownerId = Guid.CreateVersion7();
        var note = new Note
        {
            Id = Guid.CreateVersion7(),
            OwnerId = ownerId,
            Title = "My note",
            Content = "Secret content"
        };

        var claims = new[]
        {
            new Claim("sub", ownerId.ToString()),
            new Claim("permission", Permissions.NotesManageOwn)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        var req = operation switch
        {
            "Read" => NoteOperations.Read,
            "Update" => NoteOperations.Update,
            _ => NoteOperations.Delete
        };

        var context = new AuthorizationHandlerContext([req], principal, note);
        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBe(expectedAllowed);
    }

    [Theory]
    [InlineData("Read", true)]
    [InlineData("Delete", true)]
    [InlineData("Update", false)] // Moderator can NOT update someone else's note
    public async Task ModeratorWithMfa_CanReadAndDelete_ButNotUpdate(string operation, bool expectedAllowed)
    {
        var ownerId = Guid.CreateVersion7();
        var moderatorId = Guid.CreateVersion7();
        var note = new Note
        {
            Id = Guid.CreateVersion7(),
            OwnerId = ownerId,
            Title = "User note",
            Content = "User content"
        };

        var claims = new[]
        {
            new Claim("sub", moderatorId.ToString()),
            new Claim("permission", Permissions.NotesModerate),
            new Claim("amr", "otp")
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
        var req = operation switch
        {
            "Read" => NoteOperations.Read,
            "Update" => NoteOperations.Update,
            _ => NoteOperations.Delete
        };

        var context = new AuthorizationHandlerContext([req], principal, note);
        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBe(expectedAllowed);
    }

    [Fact]
    public async Task ModeratorWithoutMfa_CannotReadOrDelete()
    {
        var ownerId = Guid.CreateVersion7();
        var moderatorId = Guid.CreateVersion7();
        var note = new Note
        {
            Id = Guid.CreateVersion7(),
            OwnerId = ownerId,
            Title = "User note",
            Content = "User content"
        };

        var claims = new[]
        {
            new Claim("sub", moderatorId.ToString()),
            new Claim("permission", Permissions.NotesModerate)
            // No MFA amr claim!
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        var context = new AuthorizationHandlerContext([NoteOperations.Read], principal, note);
        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task OtherUser_CannotReadUpdateOrDelete()
    {
        var ownerId = Guid.CreateVersion7();
        var otherId = Guid.CreateVersion7();
        var note = new Note
        {
            Id = Guid.CreateVersion7(),
            OwnerId = ownerId,
            Title = "User note",
            Content = "User content"
        };

        var claims = new[]
        {
            new Claim("sub", otherId.ToString()),
            new Claim("permission", Permissions.NotesManageOwn)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        var context = new AuthorizationHandlerContext([NoteOperations.Read], principal, note);
        await _sut.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }
}
