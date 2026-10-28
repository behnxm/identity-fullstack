using System.ComponentModel.DataAnnotations;

namespace Identity.Api.Notes;

/// <summary>Request payload for creating a new note.</summary>
public sealed record CreateNoteRequest
{
    [Required, MinLength(1), MaxLength(200)]
    public required string Title { get; init; }

    [Required, MinLength(1), MaxLength(10000)]
    public required string Content { get; init; }
}

/// <summary>Request payload for updating an existing note.</summary>
public sealed record UpdateNoteRequest
{
    [Required, MinLength(1), MaxLength(200)]
    public required string Title { get; init; }

    [Required, MinLength(1), MaxLength(10000)]
    public required string Content { get; init; }
}

/// <summary>Response representing a note item.</summary>
public sealed record NoteResponse(
    Guid Id,
    Guid OwnerId,
    string Title,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
