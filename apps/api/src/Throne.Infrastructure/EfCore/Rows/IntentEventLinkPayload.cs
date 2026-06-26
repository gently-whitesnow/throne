namespace Throne.Infrastructure.EfCore.Rows;

/// <summary>
/// Serialized into the <c>link</c> JSON column on <see cref="IntentEventRow"/>.
/// Opaque from a query standpoint — the peer_intent_id used by the OR-timeline filter
/// is denormalized onto the row itself.
/// </summary>
internal sealed class IntentEventLinkPayload
{
    public string Id { get; set; } = string.Empty;
    public string FromId { get; set; } = string.Empty;
    public string ToId { get; set; } = string.Empty;
    public bool Blocking { get; set; }
    public string Author { get; set; } = string.Empty;
    public string? Rationale { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
