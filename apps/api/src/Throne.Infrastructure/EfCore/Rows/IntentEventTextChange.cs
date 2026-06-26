namespace Throne.Infrastructure.EfCore.Rows;

/// <summary>
/// Serialized into the <c>text_change</c> JSON column on <see cref="IntentEventRow"/>.
/// The shape is opaque to the relational model: nothing queries inside it, the event-
/// reader hydrates the domain delta from the whole blob.
/// </summary>
internal sealed class IntentEventTextChange
{
    public string Kind { get; set; } = string.Empty;
    public string? Snapshot { get; set; }
    public string? OldText { get; set; }
    public string? NewText { get; set; }
    public int? AfterLine { get; set; }
    public string? InsertText { get; set; }
}
