using System.Security.Claims;
using Identity.Api.Common.Constants;
using Identity.Api.Common.Results;
using Identity.Api.Persistence;
using Identity.Api.Persistence.Entities;
using Identity.Api.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Notes;

/// <summary>
/// Domain resource endpoints showcasing policy-based and resource-based authorization.
/// </summary>
public static class NotesEndpoints
{
    public static IEndpointRouteBuilder MapNotesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/notes")
            .WithTags("Notes")
            .RequireAuthorization();

        group.MapGet("/", GetMyNotesAsync)
            .RequireAuthorization(Policies.NotesManageOwn)
            .WithName("GetMyNotes")
            .WithSummary("List all notes belonging to the current user")
            .Produces<IReadOnlyList<NoteResponse>>(StatusCodes.Status200OK);

        group.MapPost("/", CreateNoteAsync)
            .RequireAuthorization(Policies.NotesManageOwn)
            .WithName("CreateNote")
            .WithSummary("Create a new personal note")
            .Produces<NoteResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}", GetNoteByIdAsync)
            .WithName("GetNoteById")
            .WithSummary("Get note details (resource-based authorization)")
            .Produces<NoteResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateNoteAsync)
            .WithName("UpdateNote")
            .WithSummary("Update note (owner only)")
            .Produces<NoteResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteNoteAsync)
            .WithName("DeleteNote")
            .WithSummary("Delete note (owner or moderator with MFA)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status403Forbidden)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetMyNotesAsync(
        ClaimsPrincipal caller,
        AppDbContext db,
        CancellationToken ct)
    {
        var sub = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        var notes = await db.Notes
            .AsNoTracking()
            .Where(n => n.OwnerId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NoteResponse(n.Id, n.OwnerId, n.Title, n.Content, n.CreatedAt, n.UpdatedAt))
            .ToListAsync(ct);

        return TypedResults.Ok(notes);
    }

    private static async Task<IResult> CreateNoteAsync(
        [FromBody] CreateNoteRequest request,
        ClaimsPrincipal caller,
        AppDbContext db,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        var sub = caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
        {
            return TypedResults.Unauthorized();
        }

        var now = timeProvider.GetUtcNow();
        var note = new Note
        {
            Id = Guid.CreateVersion7(),
            OwnerId = userId,
            Title = request.Title,
            Content = request.Content,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Notes.Add(note);
        await db.SaveChangesAsync(ct);

        var response = new NoteResponse(note.Id, note.OwnerId, note.Title, note.Content, note.CreatedAt, note.UpdatedAt);
        return TypedResults.Created($"/api/v1/notes/{note.Id}", response);
    }

    private static async Task<IResult> GetNoteByIdAsync(
        Guid id,
        ClaimsPrincipal caller,
        AppDbContext db,
        IAuthorizationService authService,
        HttpContext context,
        CancellationToken ct)
    {
        var note = await db.Notes.FindAsync([id], ct);
        if (note == null)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status404NotFound,
                "note.not_found",
                "Note not found",
                "The requested note does not exist.",
                context.Request.Path);
        }

        var authResult = await authService.AuthorizeAsync(caller, note, NoteOperations.Read);
        if (!authResult.Succeeded)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status403Forbidden,
                AuthErrorCodes.Forbidden,
                "Forbidden",
                "You do not have permission to view this note.",
                context.Request.Path);
        }

        var response = new NoteResponse(note.Id, note.OwnerId, note.Title, note.Content, note.CreatedAt, note.UpdatedAt);
        return TypedResults.Ok(response);
    }

    private static async Task<IResult> UpdateNoteAsync(
        Guid id,
        [FromBody] UpdateNoteRequest request,
        ClaimsPrincipal caller,
        AppDbContext db,
        IAuthorizationService authService,
        TimeProvider timeProvider,
        HttpContext context,
        CancellationToken ct)
    {
        var note = await db.Notes.FindAsync([id], ct);
        if (note == null)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status404NotFound,
                "note.not_found",
                "Note not found",
                "The requested note does not exist.",
                context.Request.Path);
        }

        var authResult = await authService.AuthorizeAsync(caller, note, NoteOperations.Update);
        if (!authResult.Succeeded)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status403Forbidden,
                AuthErrorCodes.Forbidden,
                "Forbidden",
                "You do not have permission to modify this note.",
                context.Request.Path);
        }

        note.Title = request.Title;
        note.Content = request.Content;
        note.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);

        var response = new NoteResponse(note.Id, note.OwnerId, note.Title, note.Content, note.CreatedAt, note.UpdatedAt);
        return TypedResults.Ok(response);
    }

    private static async Task<IResult> DeleteNoteAsync(
        Guid id,
        ClaimsPrincipal caller,
        AppDbContext db,
        IAuthorizationService authService,
        HttpContext context,
        CancellationToken ct)
    {
        var note = await db.Notes.FindAsync([id], ct);
        if (note == null)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status404NotFound,
                "note.not_found",
                "Note not found",
                "The requested note does not exist.",
                context.Request.Path);
        }

        var authResult = await authService.AuthorizeAsync(caller, note, NoteOperations.Delete);
        if (!authResult.Succeeded)
        {
            return ApiProblemDetails.CreateResult(
                StatusCodes.Status403Forbidden,
                AuthErrorCodes.Forbidden,
                "Forbidden",
                "You do not have permission to delete this note.",
                context.Request.Path);
        }

        db.Notes.Remove(note);
        await db.SaveChangesAsync(ct);

        return TypedResults.NoContent();
    }
}
