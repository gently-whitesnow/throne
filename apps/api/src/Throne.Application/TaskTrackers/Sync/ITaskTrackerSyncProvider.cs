namespace Throne.Application.TaskTrackers.Sync;

/// <summary>
/// The card-level read/write surface a tracker must expose to take part in two-way sync. Separate from
/// <see cref="ITaskTrackerConnectionProvider"/> (probe + topology) so a tracker can be connectable
/// without being syncable. Provider-neutral: every call takes the saved
/// <see cref="TaskTrackerConnectionDescriptor"/> and trades only <see cref="TaskTrackerCard"/> values.
/// </summary>
public interface ITaskTrackerSyncProvider : ITaskTrackerProvider
{
    /// <summary>Live, non-archived cards on a board, with column titles resolved.</summary>
    Task<IReadOnlyList<TaskTrackerCard>> ListBoardCardsAsync(
        TaskTrackerConnectionDescriptor connection,
        string boardId,
        CancellationToken ct);

    /// <summary>Fetch a single card by id. Returns <c>null</c> when it is gone or unreadable (404/403).</summary>
    Task<TaskTrackerCard?> GetCardAsync(
        TaskTrackerConnectionDescriptor connection,
        string cardId,
        CancellationToken ct);

    /// <summary>Write-through of changed fields. Returns the card as it stands after the update.</summary>
    Task<TaskTrackerCard> UpdateCardAsync(
        TaskTrackerConnectionDescriptor connection,
        string cardId,
        TaskTrackerCardPatch patch,
        CancellationToken ct);

    /// <summary>Child card ids of a parent (the card-children hierarchy).</summary>
    Task<IReadOnlyList<string>> ListChildCardIdsAsync(
        TaskTrackerConnectionDescriptor connection,
        string parentCardId,
        CancellationToken ct);

    Task AddChildAsync(
        TaskTrackerConnectionDescriptor connection,
        string parentCardId,
        string childCardId,
        CancellationToken ct);

    Task RemoveChildAsync(
        TaskTrackerConnectionDescriptor connection,
        string parentCardId,
        string childCardId,
        CancellationToken ct);
}
