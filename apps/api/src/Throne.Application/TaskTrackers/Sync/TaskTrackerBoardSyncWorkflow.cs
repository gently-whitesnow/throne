using Throne.Application.Ports;

namespace Throne.Application.TaskTrackers.Sync;

/// <summary>Result of syncing one board: cards whose content changed (parents to reconcile) plus a failure flag.</summary>
public sealed record BoardSyncOutcome(IReadOnlyList<string> ChangedCardIds, bool Failed);

/// <summary>
/// Polls a single board: mirrors every live card into its intent, then stubs links whose card vanished
/// from the board. Pure delta — unchanged cards are no-ops thanks to the snapshot comparison inside
/// <see cref="TaskTrackerMirrorService"/>. Child-link reconciliation is left to the tick so it can use a
/// tracker-wide card→intent map (children may live on another board).
/// </summary>
public sealed class TaskTrackerBoardSyncWorkflow(
    TaskTrackerMirrorService mirror,
    ITaskTrackerCardLinkStore linkStore)
{
    public async Task<BoardSyncOutcome> SyncBoardAsync(
        ITaskTrackerSyncProvider provider,
        TaskTrackerConnectionDescriptor descriptor,
        string boardId,
        CancellationToken ct)
    {
        IReadOnlyList<TaskTrackerCard> cards;
        try
        {
            cards = await provider.ListBoardCardsAsync(descriptor, boardId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new BoardSyncOutcome([], Failed: true);
        }

        var tracker = provider.TrackerKey;
        var changed = new List<string>();
        var liveCardIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var card in cards)
        {
            liveCardIds.Add(card.CardId);
            var existing = await linkStore.GetByCardAsync(tracker, boardId, card.CardId, ct);
            var result = await mirror.ApplyAsync(tracker, card, existing, ct);
            if (result.ContentChanged)
            {
                changed.Add(card.CardId);
            }
        }

        await StubVanishedAsync(tracker, boardId, liveCardIds, ct);
        return new BoardSyncOutcome(changed, Failed: false);
    }

    private async Task StubVanishedAsync(string tracker, string boardId, HashSet<string> liveCardIds, CancellationToken ct)
    {
        var existing = await linkStore.ListByBoardAsync(tracker, boardId, ct);
        foreach (var link in existing)
        {
            if (!link.IsStub && !liveCardIds.Contains(link.Card.CardId))
            {
                await mirror.MarkStubAsync(link, ct);
            }
        }
    }
}
