using Throne.Application.Ports;

namespace Throne.Application.TaskTrackers.Sync;

/// <summary>Counters for one poll tick, for structured host logging and tests.</summary>
public sealed record TaskTrackerSyncTickReport(int BoardsPolled, int BoardsFailed, int BoardsSkipped, int CardsChanged);

/// <summary>
/// One poll pass over every syncable tracker: for each connected tracker it walks the selected boards
/// (honouring per-board backoff), mirrors their cards, then reconciles card-children onto intent links
/// using a tracker-wide card→intent map (children may live on a sibling board). The hosted
/// <c>TaskTrackerSyncService</c> is a thin timer around this, so the whole tick is unit-testable.
/// </summary>
public sealed class TaskTrackerSyncTickWorkflow(
    IEnumerable<ITaskTrackerSyncProvider> providers,
    ITaskTrackerConnectionStore connectionStore,
    ITaskTrackerCardLinkStore linkStore,
    TaskTrackerBoardSyncWorkflow boardWorkflow,
    TaskTrackerChildLinkReconciler reconciler,
    TaskTrackerSyncBackoff backoff,
    TimeProvider clock)
{
    public async Task<TaskTrackerSyncTickReport> RunAsync(CancellationToken ct)
    {
        var polled = 0;
        var failed = 0;
        var skipped = 0;
        var changedCards = 0;

        foreach (var provider in providers)
        {
            ct.ThrowIfCancellationRequested();
            var stored = await connectionStore.GetAsync(provider.TrackerKey, ct);
            if (stored is null || stored.Selection.Count == 0)
            {
                continue;
            }

            var descriptor = new TaskTrackerConnectionDescriptor(stored.BaseUrl, stored.Token);
            var changedByCard = new List<string>();
            foreach (var board in stored.Selection)
            {
                var key = $"{provider.TrackerKey}:{board.BoardId}";
                if (backoff.ShouldSkip(key, clock.GetUtcNow()))
                {
                    skipped++;
                    continue;
                }

                var outcome = await boardWorkflow.SyncBoardAsync(provider, descriptor, board.BoardId, ct);
                if (outcome.Failed)
                {
                    failed++;
                    backoff.RecordFailure(key, clock.GetUtcNow());
                    continue;
                }

                polled++;
                backoff.RecordSuccess(key);
                changedByCard.AddRange(outcome.ChangedCardIds);
            }

            changedCards += changedByCard.Count;
            await ReconcileChildrenAsync(provider, descriptor, changedByCard, ct);
        }

        return new TaskTrackerSyncTickReport(polled, failed, skipped, changedCards);
    }

    private async Task ReconcileChildrenAsync(
        ITaskTrackerSyncProvider provider,
        TaskTrackerConnectionDescriptor descriptor,
        List<string> changedCardIds,
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
