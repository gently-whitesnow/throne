namespace Throne.Domain.TaskTrackers;

/// <summary>
/// One-to-one mirror binding between a Throne intent and an external task-tracker card.
/// Throne keeps only the stable identifiers (<see cref="TaskTrackerCardLink"/>) plus the minimal
/// sync bookkeeping needed to round-trip without merges: the last snapshot we observed/pushed (for
/// echo suppression — last-write-wins never re-applies a value it just wrote) and the upstream
/// change cursors (<see cref="CardUpdatedAt"/> / <see cref="ColumnChangedAt"/>) used to skip
/// unchanged cards during polling. Mutable card content lives in the tracker; the intent text is the
/// mirror of it.
/// </summary>
public sealed class CardSyncLink
{
    private CardSyncLink(string intentId, TaskTrackerCardLink card, CardSyncLinkSnapshot snapshot, CardSyncLinkCursors cursors, string state, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        IntentId = intentId;
        Card = card;
        Snapshot = snapshot;
        Cursors = cursors;
        State = state;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public string IntentId { get; }
    public TaskTrackerCardLink Card { get; }
    public CardSyncLinkSnapshot Snapshot { get; private set; }
    public CardSyncLinkCursors Cursors { get; private set; }
    public string State { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsStub => string.Equals(State, CardSyncLinkState.Stub, StringComparison.Ordinal);

    public static CardSyncLink Create(
        string intentId,
        TaskTrackerCardLink card,
        CardSyncLinkSnapshot snapshot,
        CardSyncLinkCursors cursors,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(cursors);
        return new CardSyncLink(intentId, card, snapshot, cursors, CardSyncLinkState.Linked, now, now);
    }

    public static CardSyncLink Restore(
        string intentId,
        TaskTrackerCardLink card,
        CardSyncLinkSnapshot snapshot,
        CardSyncLinkCursors cursors,
        string state,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(cursors);
        if (!CardSyncLinkState.IsKnown(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), $"Unknown card-sync-link state: {state}.");
        }

        return new CardSyncLink(intentId, card, snapshot, cursors, state, createdAt, updatedAt);
    }

    /// <summary>
    /// Records the latest snapshot/cursors observed from (or pushed to) the tracker and re-links the
    /// mirror. Callers update the intent text separately; this only moves the bookkeeping so the next
    /// poll and the next write-through both see a fresh baseline.
    /// </summary>
    public void RecordSnapshot(CardSyncLinkSnapshot snapshot, CardSyncLinkCursors cursors, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(cursors);
        Snapshot = snapshot;
        Cursors = cursors;
        State = CardSyncLinkState.Linked;
        UpdatedAt = now;
    }

    /// <summary>
    /// Updates only the snapshot baseline after a write-through push, keeping the upstream cursors
    /// untouched (the next poll re-reads them from the card's own <c>updated</c>).
    /// </summary>
    public void RecordPushedSnapshot(CardSyncLinkSnapshot snapshot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Snapshot = snapshot;
        State = CardSyncLinkState.Linked;
        UpdatedAt = now;
    }

    /// <summary>Card vanished upstream or became unreadable: keep the mirror, stop overwriting it.</summary>
    public void MarkStub(DateTimeOffset now)
    {
        if (IsStub)
        {
            return;
        }

        State = CardSyncLinkState.Stub;
        UpdatedAt = now;
    }
}
