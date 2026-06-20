namespace Throne.Domain.Intents.Linking;

/// <summary>
/// A directed edge between two intents. The graph is orthogonal to <c>Intent.text</c>:
/// creating or deleting a link does NOT bump <c>current_version</c> nor
/// <c>updated_at</c> — same posture as <see cref="Intent.MoveTo"/>.
///
/// <c>Blocking</c> marks hard dependency edges; non-blocking edges are soft context /
/// provenance edges in the same forward direction.
/// </summary>
public sealed record IntentLink(
    string Id,
    IntentId FromId,
    IntentId ToId,
    bool Blocking,
    IntentLinkAuthor Author,
    string? Rationale,
    DateTimeOffset CreatedAt)
{
    public static IntentLink Create(
        string id,
        IntentId fromId,
        IntentId toId,
        bool blocking,
        IntentLinkAuthor author,
        string? rationale,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        if (string.Equals(fromId.Value, toId.Value, StringComparison.Ordinal))
        {
            throw new ArgumentException("A link cannot point an intent to itself.", nameof(toId));
        }

        var trimmed = string.IsNullOrWhiteSpace(rationale) ? null : rationale.Trim();
        return new IntentLink(id, fromId, toId, blocking, author, trimmed, createdAt);
    }
}
