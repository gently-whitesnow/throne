using Microsoft.Extensions.Logging;
using Throne.Application.Events;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Linking;
using Throne.Domain.TaskTrackers;

namespace Throne.Application.TaskTrackers.Sync;

/// <summary>
/// Pushes Throne-side changes to a mirrored card the moment they commit (ADR-0008 write-through,
/// last-write-wins). Title rides <c>IntentTitleChanged</c> and description rides <c>IntentTextChanged</c> —
/// each pushes only its own card field so an independent title rename never clobbers the description.
/// Card-children ride <c>IntentLinkAdded</c>/<c>IntentLinkRemoved</c>; closing a stub mirror clears its
/// local link. Echoes are suppressed (the snapshot already equals the intent field; the Kaiten child is
/// already in the desired state). Upstream failures are swallowed and logged — a tracker outage must
/// never fail the operator's local edit.
/// </summary>
public sealed partial class TaskTrackerWriteThroughHandler(
    ITaskTrackerCardLinkStore linkStore,
    ITaskTrackerConnectionStore connectionStore,
    IEnumerable<ITaskTrackerSyncProvider> providers,
    TimeProvider clock,
    ILogger<TaskTrackerWriteThroughHandler> logger) : IDomainEventHandler
{
    public async Task HandleAsync(IDomainEvent evt, CancellationToken ct)
    {
        try
        {
            switch (evt)
            {
                case IntentTitleChanged title:
                    await PushTitleAsync(title.Intent, ct);
                    break;
                case IntentTextChanged text:
                    await PushTextAsync(text.Intent, ct);
                    break;
                case IntentLinkAdded added:
                    await PushChildAsync(added.Link, present: true, ct);
                    break;
                case IntentLinkRemoved removed:
                    await PushChildAsync(removed.Link, present: false, ct);
                    break;
                case IntentStatusChanged status when IntentStatusNames.IsClosing(status.Intent.State.Status):
                    await CleanupOnCloseAsync(status.Intent.Id.Value, ct);
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogPushFailed(logger, ex);
        }
    }

    private async Task PushTitleAsync(Intent intent, CancellationToken ct)
    {
        var link = await linkStore.GetByIntentAsync(intent.Id.Value, ct);
        var title = intent.State.Title;
        // A linked intent is required to keep a non-empty title (enforced on the write-path); skip
        // defensively rather than push an empty title that every tracker would reject.
        if (link is null || link.IsStub || string.IsNullOrWhiteSpace(title)
            || string.Equals(link.Snapshot.Title, title, StringComparison.Ordinal))
        {
            return;
        }

        var resolved = await ResolveAsync(link.Card.Tracker, ct);
        if (resolved is null)
        {
            return;
        }

        var (provider, descriptor) = resolved.Value;
        await provider.UpdateCardAsync(descriptor, link.Card.CardId, new TaskTrackerCardPatch(Title: title), ct);
        link.RecordPushedSnapshot(link.Snapshot with { Title = title }, clock.GetUtcNow());
        await linkStore.SaveAsync(link, ct);
    }

    private async Task PushTextAsync(Intent intent, CancellationToken ct)
    {
        var link = await linkStore.GetByIntentAsync(intent.Id.Value, ct);
        var text = intent.State.Text;
        // Echo suppression: the live text already mirrors the snapshot's description (the title seeds
        // the text when the card description is blank).
        if (link is null || link.IsStub
            || string.Equals(text, CardMirror.TextOf(link.Snapshot.Title, link.Snapshot.Description), StringComparison.Ordinal))
        {
            return;
        }

        var resolved = await ResolveAsync(link.Card.Tracker, ct);
        if (resolved is null)
        {
            return;
        }

        var (provider, descriptor) = resolved.Value;
        await provider.UpdateCardAsync(descriptor, link.Card.CardId, new TaskTrackerCardPatch(Description: text), ct);
        link.RecordPushedSnapshot(link.Snapshot with { Description = text }, clock.GetUtcNow());
        await linkStore.SaveAsync(link, ct);
    }

    private async Task PushChildAsync(IntentLink edge, bool present, CancellationToken ct)
    {
        var parentLink = await linkStore.GetByIntentAsync(edge.FromId.Value, ct);
        var childLink = await linkStore.GetByIntentAsync(edge.ToId.Value, ct);
        if (!BothLiveOnSameTracker(parentLink, childLink))
        {
            return;
        }

        var parent = parentLink!.Card;
        var child = childLink!.Card;
        var resolved = await ResolveAsync(parent.Tracker, ct);
        if (resolved is null)
        {
            return;
        }

        var (provider, descriptor) = resolved.Value;
        var children = await provider.ListChildCardIdsAsync(descriptor, parent.CardId, ct);
        var alreadyPresent = children.Contains(child.CardId, StringComparer.Ordinal);
        if (present && !alreadyPresent)
        {
            await provider.AddChildAsync(descriptor, parent.CardId, child.CardId, ct);
        }
        else if (!present && alreadyPresent)
        {
            await provider.RemoveChildAsync(descriptor, parent.CardId, child.CardId, ct);
        }
    }

    private static bool BothLiveOnSameTracker(CardSyncLink? parent, CardSyncLink? child) =>
        parent is { IsStub: false } &&
        child is { IsStub: false } &&
        string.Equals(parent.Card.Tracker, child.Card.Tracker, StringComparison.Ordinal);

    private async Task CleanupOnCloseAsync(string intentId, CancellationToken ct)
    {
        var link = await linkStore.GetByIntentAsync(intentId, ct);
        if (link is { IsStub: true })
        {
            await linkStore.DeleteByIntentAsync(intentId, ct);
        }
    }

    private async Task<(ITaskTrackerSyncProvider Provider, TaskTrackerConnectionDescriptor Descriptor)?> ResolveAsync(
        string tracker, CancellationToken ct)
    {
        var provider = providers.FirstOrDefault(p => string.Equals(p.TrackerKey, tracker, StringComparison.Ordinal));
        if (provider is null)
        {
            return null;
        }

        var stored = await connectionStore.GetAsync(tracker, ct);
        return stored is null ? null : (provider, new TaskTrackerConnectionDescriptor(stored.BaseUrl, stored.Token));
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Task-tracker write-through push failed; intent change kept, tracker left unsynced until next poll.")]
    private static partial void LogPushFailed(ILogger logger, Exception exception);
}
