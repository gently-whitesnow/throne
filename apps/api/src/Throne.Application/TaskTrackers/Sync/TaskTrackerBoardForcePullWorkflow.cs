using Throne.Application.Ports;

namespace Throne.Application.TaskTrackers.Sync;

public enum BoardForcePullStatus
{
    Synced,
    Unavailable,
    NotConnected,
}

public sealed record BoardForcePullResult(BoardForcePullStatus Status, int CardsChanged);

/// <summary>
/// Board-scoped «Обновить»: runs the same delta poll the background tick runs for a single board, but
/// on demand and without the per-board backoff. Reuses <see cref="TaskTrackerBoardSyncWorkflow"/> and the
/// child-link reconciler so a manual refresh and the periodic loop can never diverge. This is the
/// existing polling path with a "run now" trigger — not a separate bulk fetch.
/// </summary>
public sealed class TaskTrackerBoardForcePullWorkflow(
    IEnumerable<ITaskTrackerSyncProvider> providers,
    ITaskTrackerConnectionStore connectionStore,
    ITaskTrackerCardLinkStore linkStore,
    TaskTrackerBoardSyncWorkflow boardWorkflow,
    TaskTrackerChildLinkReconciler reconciler)
{
    public async Task<BoardForcePullResult> ForcePullAsync(string tracker, string boardId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tracker);
        ArgumentException.ThrowIfNullOrWhiteSpace(boardId);

        var provider = providers.FirstOrDefault(p => string.Equals(p.TrackerKey, tracker, StringComparison.Ordinal));
        var stored = provider is null ? null : await connectionStore.GetAsync(tracker, ct);
        if (provider is null || stored is null)
        {
            return new BoardForcePullResult(BoardForcePullStatus.NotConnected, 0);
        }

        var descriptor = new TaskTrackerConnectionDescriptor(stored.BaseUrl, stored.Token);
        var outcome = await boardWorkflow.SyncBoardAsync(provider, descriptor, boardId, ct);
        if (outcome.Failed)
        {
            return new BoardForcePullResult(BoardForcePullStatus.Unavailable, 0);
        }

        await ReconcileChildrenAsync(provider, descriptor, outcome.ChangedCardIds, ct);
        return new BoardForcePullResult(BoardForcePullStatus.Synced, outcome.ChangedCardIds.Count);
    }

    private async Task ReconcileChildrenAsync(
        ITaskTrackerSyncProvider provider,
        TaskTrackerConnectionDescriptor descriptor,
        IReadOnlyList<string> changedCardIds,
        CancellationToken ct)
    {
        if (changedCardIds.Count == 0)
        {
            return;
        }

        var links = await linkStore.ListByTrackerAsync(provider.TrackerKey, ct);
        var cardToIntent = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var link in links)
        {
            cardToIntent[link.Card.CardId] = link.IntentId;
        }

        foreach (var cardId in changedCardIds)
        {
            if (cardToIntent.TryGetValue(cardId, out var intentId))
            {
                await reconciler.ReconcileAsync(provider, descriptor, cardId, intentId, cardToIntent, ct);
            }
        }
    }
}
