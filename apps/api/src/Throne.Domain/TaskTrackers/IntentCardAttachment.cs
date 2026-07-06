using Throne.Domain.Intents;

namespace Throne.Domain.TaskTrackers;

/// <summary>
/// First-class aggregate (ADR-0052): a task-tracker card attached to an intent as read-only context. The
/// attachment stores a non-authoritative <see cref="CardSnapshot"/> that is never written back upstream;
/// the card never re-classifies the intent (no divert, no auto-status).
///
/// Invariants:
/// <list type="bullet">
///   <item>The tuple <c>(IntentId, Coordinate)</c> is unique (enforced by the store).</item>
///   <item>Identity — <see cref="Id"/>, <see cref="IntentId"/>, <see cref="Coordinate"/>,
///         <see cref="CreatedAt"/> — is immutable after creation.</item>
///   <item><see cref="IntentCardAttachmentState.Availability"/> is one of
///         <see cref="CardAvailabilityNames"/>; a degraded refresh keeps the previous snapshot and only
///         flips availability.</item>
/// </list>
///
/// Per ADR-0025 the aggregate is rich-DDD: transitions live inside this class as <c>Create</c>/
/// <c>Restore</c> factories and instance mutators, and the mutable portion is grouped into
/// <see cref="IntentCardAttachmentState"/> so transitions read as <c>State = State with { … }</c>.
/// </summary>
public sealed class IntentCardAttachment
{
    private IntentCardAttachment(
        CardAttachmentId id,
        IntentId intentId,
        CardCoordinate coordinate,
        DateTimeOffset createdAt,
        IntentCardAttachmentState state)
    {
        Id = id;
        IntentId = intentId;
        Coordinate = coordinate;
        CreatedAt = createdAt;
        State = state;
    }

    public CardAttachmentId Id { get; }
    public IntentId IntentId { get; }
    public CardCoordinate Coordinate { get; }
    public DateTimeOffset CreatedAt { get; }
    public IntentCardAttachmentState State { get; private set; }

    /// <summary>
    /// Attach a card that was just pulled successfully: the snapshot is authoritative, so the attachment
    /// starts <see cref="CardAvailabilityNames.Available"/>.
    /// </summary>
    public static IntentCardAttachment Create(
        CardAttachmentId id,
        IntentId intentId,
        CardCoordinate coordinate,
        CardSnapshot snapshot,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        ArgumentNullException.ThrowIfNull(snapshot);

        var state = new IntentCardAttachmentState(snapshot, CardAvailabilityNames.Available, now);
        return new IntentCardAttachment(id, intentId, coordinate, now, state);
    }

    /// <summary>
    /// Rehydrate an attachment from persistence. The availability field is re-checked so a tampered row
    /// fails fast.
    /// </summary>
    public static IntentCardAttachment Restore(IntentCardAttachmentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.Coordinate);
        ArgumentNullException.ThrowIfNull(snapshot.Snapshot);
        EnsureKnownAvailability(snapshot.Availability);

        var state = new IntentCardAttachmentState(
            snapshot.Snapshot, snapshot.Availability, snapshot.UpdatedAt);
        return new IntentCardAttachment(
            snapshot.Id, snapshot.IntentId, snapshot.Coordinate, snapshot.CreatedAt, state);
    }

    /// <summary>
    /// Record a fresh successful pull: replace the snapshot and flip availability back to
    /// <see cref="CardAvailabilityNames.Available"/>. Used by both re-attach (idempotent) and refresh.
    /// </summary>
    public void ApplySnapshot(CardSnapshot snapshot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        State = State with
        {
            Snapshot = snapshot,
            Availability = CardAvailabilityNames.Available,
            UpdatedAt = now,
        };
    }

    /// <summary>
    /// Degrade availability without losing the last snapshot: a refresh that could not read the card sets
    /// <see cref="CardAvailabilityNames.Unavailable"/> (tracker unreachable / not connected) or
    /// <see cref="CardAvailabilityNames.Gone"/> (card vanished / forbidden upstream). The snapshot is kept
    /// so the operator still sees the last-known content.
    /// </summary>
    public void MarkUnavailable(string availability, DateTimeOffset now)
    {
        if (availability is not (CardAvailabilityNames.Unavailable or CardAvailabilityNames.Gone))
        {
            throw new ArgumentOutOfRangeException(
                nameof(availability),
                availability,
                $"MarkUnavailable accepts only '{CardAvailabilityNames.Unavailable}' or '{CardAvailabilityNames.Gone}'.");
        }

        State = State with { Availability = availability, UpdatedAt = now };
    }

    private static void EnsureKnownAvailability(string availability)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(availability);
        if (!CardAvailabilityNames.IsKnown(availability))
        {
            throw new ArgumentOutOfRangeException(
                nameof(availability), availability, $"Unknown availability: {availability}.");
        }
    }
}
