using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.TaskTrackers;
using Throne.Domain.TextVersions;

namespace Throne.Application.TaskTrackers.Sync;

/// <summary>Outcome of mirroring a single card into its intent.</summary>
public sealed record MirrorApplyResult(string IntentId, bool Created, bool ContentChanged);

/// <summary>
/// Applies one tracker card onto its mirror intent: creates the intent on first sight, otherwise pulls
/// the card's title into the intent title and the card's description into the intent text — but only the
/// fields that actually differ from the snapshot baseline (last-write-wins, no merge). The link's
/// snapshot is persisted <em>before</em> the intent writes so the write-through handler, firing on the
/// resulting <c>IntentTitleChanged</c>/<c>IntentTextChanged</c>, sees the new baseline and suppresses the
/// echo back to the tracker. Reused by both the poll loop and force-pull so the two paths can never drift.
/// </summary>
public sealed class TaskTrackerMirrorService(
    IIntentRepository repository,
    IIntentOrderingRepository ordering,
    ITaskTrackerCardLinkStore linkStore,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    private const string TaskTrackerSyncSource = "task_tracker_sync";
    private const string ArchivedByTrackerReason = "архивировано таск-трекером";

    public async Task<MirrorApplyResult> ApplyAsync(string tracker, TaskTrackerCard card, CardSyncLink? existing, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(card);
        var now = clock.GetUtcNow();
        var snapshot = new CardSyncLinkSnapshot(card.Title, card.Description, card.ColumnId, card.ColumnTitle);
        var cursors = new CardSyncLinkCursors(card.UpdatedAt, card.ColumnChangedAt, now, card.RevisionTag);

        if (existing is null)
        {
            return await CreateAsync(tracker, card, snapshot, cursors, now, ct);
        }

        var intent = await repository.GetByIdAsync(new IntentId(existing.IntentId), ct);
        if (intent is null)
        {
            // The operator deleted the mirror intent; drop the stale link and re-mirror fresh.
            await linkStore.DeleteByIntentAsync(existing.IntentId, ct);
            return await CreateAsync(tracker, card, snapshot, cursors, now, ct);
        }

        var newText = CardMirror.TextOf(card.Title, card.Description);
        var titleChanged = !string.Equals(intent.State.Title, card.Title, StringComparison.Ordinal);
        var textChanged = !string.Equals(intent.State.Text, newText, StringComparison.Ordinal);
        if (!titleChanged && !textChanged)
        {
            existing.RecordSnapshot(snapshot, cursors, now);
            await linkStore.SaveAsync(existing, ct);
            return new MirrorApplyResult(existing.IntentId, Created: false, ContentChanged: false);
        }

        // Persist the new baseline first so the write-through handler, firing on the writes below,
        // sees the card's values already mirrored and suppresses the echo back to the tracker.
        existing.RecordSnapshot(snapshot, cursors, now);
        await linkStore.SaveAsync(existing, ct);

        // Title is metadata (no version bump); apply it first so the text CAS still sees the same version.
        if (titleChanged)
        {
            await unitOfWork.ExecuteAsync(
                inner => repository.SetTitleAsync(intent.Id, intent.State.CurrentVersion, card.Title, now, inner),
                ct);
        }
        if (textChanged)
        {
            await unitOfWork.ExecuteAsync(
                inner => repository.ReplaceTextAsync(
                    intent.Id, intent.State.CurrentVersion, intent.State.Text, newText, TextVersionAuthor.Agent, now, inner),
                ct);
        }
        return new MirrorApplyResult(existing.IntentId, Created: false, ContentChanged: true);
    }

    public async Task MarkStubAsync(CardSyncLink link, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(link);
        if (link.IsStub)
        {
            return;
        }

        link.MarkStub(clock.GetUtcNow());
        await linkStore.SaveAsync(link, ct);
    }

    // A card that left the board's Live list is de-facto final upstream (Kaiten archives it after the
    // fact). Closing the mirror as reject — not done — reflects «abandoned, not finished». The stub is
    // saved before the reject so the write-through CleanupOnClose (which fires on IntentStatusChanged and
    // only clears stub links) deletes the link, collapsing the board counter/list for free.
    // Idempotent on both invariants (link == stub AND intent closed) so re-ticks — and the legacy
    // active-intent-with-stub-link rows — converge instead of being skipped.
    public async Task StubAndCloseVanishedAsync(CardSyncLink link, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(link);
        await MarkStubAsync(link, ct);

        var intent = await repository.GetByIdAsync(new IntentId(link.IntentId), ct);
        if (intent is null || IntentStatusNames.IsClosing(intent.State.Status))
        {
            return;
        }

        _ = await unitOfWork.ExecuteAsync(
            inner => repository.SetStatusAsync(
                intent.Id,
                IntentStatusNames.Reject,
                appendText: null,
                reason: ArchivedByTrackerReason,
                IntentTrainingAuthor.System,
                TaskTrackerSyncSource,
                clock.GetUtcNow(),
                inner),
            ct);
    }

    private async Task<MirrorApplyResult> CreateAsync(
        string tracker, TaskTrackerCard card, CardSyncLinkSnapshot snapshot, CardSyncLinkCursors cursors, DateTimeOffset now, CancellationToken ct)
    {
        var id = IntentId.New();
        var text = CardMirror.TextOf(card.Title, card.Description);
        var sortKey = await NextTopSortKeyAsync(ct);
        await unitOfWork.ExecuteAsync(
            async inner =>
            {
                var intent = Intent.Create(id, text, null, now, sortKey: sortKey, title: card.Title);
                var version = TextVersion.CreateSnapshot(
                    id: Guid.NewGuid().ToString("N"),
                    ownerKind: TextVersionOwnerKind.Intent,
                    ownerId: id.Value,
                    snapshot: intent.State.Text,
                    changedAt: now,
                    changedBy: TextVersionAuthor.Agent);
                var statusChange = IntentStatusChange.Create(
                    id: Guid.NewGuid().ToString("N"),
                    intentId: id,
                    intentVersionAtWrite: intent.State.CurrentVersion,
                    fromStatus: intent.State.Status,
                    toStatus: intent.State.Status,
                    source: TaskTrackerSyncSource,
                    createdAt: now,
                    createdBy: TextVersionAuthorMapping.ToTrainingAuthor(TextVersionAuthor.Agent));
                return await repository.CreateAsync(intent, version, statusChange, [], inner);
            },
            ct);

        var link = CardSyncLink.Create(id.Value, new TaskTrackerCardLink(tracker, card.BoardId, card.CardId), snapshot, cursors, now);
        await linkStore.SaveAsync(link, ct);
        return new MirrorApplyResult(id.Value, Created: true, ContentChanged: true);
    }

    private async Task<string> NextTopSortKeyAsync(CancellationToken ct)
    {
        var currentMin = await ordering.GetMinSortKeyAsync(ct);
        return FractionalIndex.Between(before: null, after: string.IsNullOrEmpty(currentMin) ? null : currentMin);
    }
}
