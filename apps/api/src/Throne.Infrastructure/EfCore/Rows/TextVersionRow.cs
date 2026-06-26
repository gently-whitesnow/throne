namespace Throne.Infrastructure.EfCore.Rows;

/// <summary>
/// Persistence POCO for the <c>text_versions</c> table. Delta fields are flat columns
/// (not a single JSON blob) so the schema reproduces <c>TextVersionDocument</c> exactly —
/// both halves of the migration store the same column set and queries that ever want to
/// filter by individual delta fields stay open.
/// </summary>
internal sealed class TextVersionRow
{
    public string Id { get; set; } = string.Empty;
    public string OwnerKind { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string? Snapshot { get; set; }
    public string? OldText { get; set; }
    public string? NewText { get; set; }
    public int? AfterLine { get; set; }
    public string? InsertText { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
}
