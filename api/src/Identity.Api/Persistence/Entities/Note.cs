namespace Identity.Api.Persistence.Entities;

/// <summary>
/// Domain resource for demonstrating resource-based authorization.
/// </summary>
public class Note
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid OwnerId { get; set; }

    public required string Title { get; set; }

    public required string Content { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public AppUser Owner { get; set; } = null!;
}
