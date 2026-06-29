using Throne.Application.Ports;
using Throne.Domain.TaskTrackers;

namespace Throne.Application.TaskTrackers.Sync;

/// <summary>Result of syncing one board: cards whose content changed (parents to reconcile) plus a failure flag.</summary>
public sealed record BoardSyncOutcome(IReadOnlyList<string> ChangedCardIds, bool Failed);

/// <summary>
/// Polls a single board: discovers live cards via the cheap list call, escalates to the detail
/// endpoint only for cards whose provider revision tag advanced relative to the persisted cursor,
/// then mirrors them into their intents and stubs links whose card vanished from the board.
/// <para>
/// Kaiten (and other trackers that expose a list/detail-asymmetric card shape — list-row omits
/// expensive fields like <c>description</c>) requires the escalation: without it the mirror would
/// see <c>description=null</c> on every list-row and never bring the body across. <see cref="TaskTrackerCard.RevisionTag"/>
/// is the opaque provider-supplied delta cursor (Kaiten — <c>card.version</c>); a missing tag on
/// either side forces detail-fetch (degrade-gracefully). Child-link reconciliation is left to the
/// tick so it can use a tracker-wide card→intent map (children may live on another board).
/// </para>
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
        foreach (var listCard in cards)
        {
            liveCardIds.Add(listCard.CardId);
            var existing = await linkStore.GetByCardAsync(tracker, boardId, listCard.CardId, ct);
            if (!ShouldFetchDetail(existing, listCard))
            {
                continue;
            }

            var fullCard = await TryGetDetailAsync(provider, descriptor, listCard, ct);
            if (fullCard is null)
            {
                continue;
            }

            var result = await mirror.ApplyAsync(tracker, fullCard, existing, ct);
            if (result.ContentChanged)
            {
                changed.Add(fullCard.CardId);
            }
        }

        await StubVanishedAsync(tracker, boardId, liveCardIds, ct);
        return new BoardSyncOutcome(changed, Failed: false);
    }

    private static bool ShouldFetchDetail(CardSyncLink? existing, TaskTrackerCard listCard)
    {
        if (existing is null)
        {
            return true;
        }

        var listTag = listCard.RevisionTag;
        var storedTag = existing.Cursors.RevisionTag;
        if (listTag is null || storedTag is null)
        {
            return true;
        }

        return !string.Equals(listTag, storedTag, StringComparison.Ordinal);
    }

    private static async Task<TaskTrackerCard?> TryGetDetailAsync(
        ITaskTrackerSyncProvider provider,
        TaskTrackerConnectionDescriptor descriptor,
        TaskTrackerCard listCard,
        CancellationToken ct)
    {
        try
        {
            return await provider.GetCardAsync(descriptor, listCard.CardId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
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
