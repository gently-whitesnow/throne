namespace Throne.Domain.Intents.Linking;

/// <summary>
/// A directed edge between two intents. The graph is orthogonal to <c>Intent.text</c>:
/// creating or deleting a link does NOT bump <c>current_version</c> nor
/// <c>updated_at</c> — same posture as <see cref="Intent.MoveTo"/>.
///
/// Mirror roles (e.g. <c>blocked_by</c> for <c>blocks</c>, <c>source_of</c> for
/// <c>derived_from</c>) are computed projections over the <c>to_id</c> index, never
/// stored as separate documents.
/// </summary>
public sealed record IntentLink(
    string Id,
    IntentId FromId,
    IntentId ToId,
    string Type,
    IntentLinkAuthor Author,
    string? Rationale,
    DateTimeOffset CreatedAt)
{
    public static IntentLink Create(
        string id,
        IntentId fromId,
        IntentId toId,
        string type,
        IntentLinkAuthor author,
        string? rationale,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        if (!IntentLinkType.IsKnown(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), $"Unknown intent link type: {type}.");
        }

        if (string.Equals(fromId.Value, toId.Value, StringComparison.Ordinal))
        {
            throw new ArgumentException("A link cannot point an intent to itself.", nameof(toId));
        }

        var trimmed = string.IsNullOrWhiteSpace(rationale) ? null : rationale.Trim();
        return new IntentLink(id, fromId, toId, type, author, trimmed, createdAt);
    }
}
