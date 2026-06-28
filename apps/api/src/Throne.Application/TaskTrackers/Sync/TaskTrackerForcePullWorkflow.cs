using Throne.Application.Ports;

namespace Throne.Application.TaskTrackers.Sync;

public enum ForcePullStatus
{
    NotLinked,
    Refreshed,
    Stubbed,
    Unavailable,
}

public sealed record ForcePullResult(ForcePullStatus Status, string? Tracker, string? CardId, string? State);

/// <summary>
/// Synchronous "pull this card now" used on intent open and behind the "Обновить" action. Fetches the
/// card by id straight from the tracker: a 404/403 (null) stubs the mirror, otherwise it re-applies the
/// card and reconciles its children. Shares <see cref="TaskTrackerMirrorService"/> with the poll loop so
/// open-time and background sync can never disagree.
/// </summary>
public sealed class TaskTrackerForcePullWorkflow(
    ITaskTrackerCardLinkStore linkStore,
    ITaskTrackerConnectionStore connectionStore,
    IEnumerable<ITaskTrackerSyncProvider> providers,
    TaskTrackerMirrorService mirror,
    TaskTrackerChildLinkReconciler reconciler)
{
    public async Task<ForcePullResult> ForcePullAsync(string intentId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        var link = await linkStore.GetByIntentAsync(intentId, ct);
        if (link is null)
        {
            return new ForcePullResult(ForcePullStatus.NotLinked, null, null, null);
        }

        var tracker = link.Card.Tracker;
        var provider = providers.FirstOrDefault(p => string.Equals(p.TrackerKey, tracker, StringComparison.Ordinal));
        var stored = provider is null ? null : await connectionStore.GetAsync(tracker, ct);
        if (provider is null || stored is null)
        {
            return new ForcePullResult(ForcePullStatus.Unavailable, tracker, link.Card.CardId, link.State);
        }

        var descriptor = new TaskTrackerConnectionDescriptor(stored.BaseUrl, stored.Token);
        TaskTrackerCard? card;
        try
        {
            card = await provider.GetCardAsync(descriptor, link.Card.CardId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ForcePullResult(ForcePullStatus.Unavailable, tracker, link.Card.CardId, link.State);
        }

        if (card is null)
        {
            await mirror.MarkStubAsync(link, ct);
            return new ForcePullResult(ForcePullStatus.Stubbed, tracker, link.Card.CardId, Domain.TaskTrackers.CardSyncLinkState.Stub);
        }

        await mirror.ApplyAsync(tracker, card, link, ct);
        await ReconcileChildrenAsync(provider, descriptor, tracker, link.Card.CardId, intentId, ct);
        return new ForcePullResult(ForcePullStatus.Refreshed, tracker, link.Card.CardId, Domain.TaskTrackers.CardSyncLinkState.Linked);
    }

    private async Task ReconcileChildrenAsync(
        ITaskTrackerSyncProvider provider,
        TaskTrackerConnectionDescriptor descriptor,
        string tracker,
        string parentCardId,
        string parentIntentId,
        CancellationToken ct)
    {
        var links = await linkStore.ListByTrackerAsync(tracker, ct);
        var cardToIntent = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var link in links)
        {
            cardToIntent[link.Card.CardId] = link.IntentId;
        }

        await reconciler.ReconcileAsync(provider, descriptor, parentCardId, parentIntentId, cardToIntent, ct);
    }
}
