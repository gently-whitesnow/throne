namespace Throne.Infrastructure.EfCore.Rows;

/// <summary>
/// Append-only entry in <c>intent_events</c>. <c>text_change</c> and <c>link</c>
/// subdocuments are stored as JSON columns: nothing queries inside them and keeping
/// them opaque preserves the SQLite schema shape one-for-one.
/// </summary>
internal sealed class IntentEventRow
{
    public string Id { get; set; } = string.Empty;
    public string IntentId { get; set; } = string.Empty;
    public string? PeerIntentId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public int? Version { get; set; }
    public IntentEventTextChange? TextChange { get; set; }
    public IntentEventLinkPayload? Link { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}
