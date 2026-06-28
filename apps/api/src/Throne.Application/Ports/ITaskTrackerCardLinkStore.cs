using Throne.Domain.TaskTrackers;

namespace Throne.Application.Ports;

/// <summary>
/// Persistence for the intent ↔ card mirror bindings. Deliberately tiny: the link rows are the only
/// local state the sync axis keeps (ADR-0008 "minimum local state"), everything else is read through
/// the provider on demand.
/// </summary>
public interface ITaskTrackerCardLinkStore
{
    Task<CardSyncLink?> GetByIntentAsync(string intentId, CancellationToken ct);

    Task<CardSyncLink?> GetByCardAsync(string tracker, string boardId, string cardId, CancellationToken ct);

    /// <summary>All links for a tracker. Backs child-link reconciliation, which needs the card↔intent map.</summary>
    Task<IReadOnlyList<CardSyncLink>> ListByTrackerAsync(string tracker, CancellationToken ct);

    /// <summary>Links for one board — used to detect cards that disappeared from a board poll.</summary>
    Task<IReadOnlyList<CardSyncLink>> ListByBoardAsync(string tracker, string boardId, CancellationToken ct);

    Task SaveAsync(CardSyncLink link, CancellationToken ct);

    /// <summary>Removes the mirror binding (local-only). Idempotent. Does not touch the upstream card.</summary>
    Task DeleteByIntentAsync(string intentId, CancellationToken ct);
}
