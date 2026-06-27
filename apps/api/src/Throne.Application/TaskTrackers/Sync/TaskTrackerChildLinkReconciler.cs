using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Linking;

namespace Throne.Application.TaskTrackers.Sync;

/// <summary>
/// Mirrors a card's children onto directed intent links (ADR-0018: parent → child = from → to). Only
/// sync-authored edges (<see cref="CardChildRationale"/>) are pruned, so an operator's own links
/// between mirror intents are never touched. Adds fire <c>IntentLinkAdded</c> and removes fire
/// <c>IntentLinkRemoved</c>; the write-through handler suppresses the echo because the Kaiten child is
/// already in (or already out of) the desired state.
/// </summary>
public sealed class TaskTrackerChildLinkReconciler(
    IIntentLinkRepository linkRepository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public const string CardChildRationale = "kaiten:card-child";

    public async Task ReconcileAsync(
        ITaskTrackerSyncProvider provider,
        TaskTrackerConnectionDescriptor descriptor,
        string parentCardId,
        string parentIntentId,
        IReadOnlyDictionary<string, string> cardToIntent,
        CancellationToken ct)
    {
        var childCardIds = await provider.ListChildCardIdsAsync(descriptor, parentCardId, ct);
        var desired = new HashSet<string>(StringComparer.Ordinal);
        foreach (var childCardId in childCardIds)
        {
            if (cardToIntent.TryGetValue(childCardId, out var childIntentId) &&
                !string.Equals(childIntentId, parentIntentId, StringComparison.Ordinal))
            {
                desired.Add(childIntentId);
            }
        }

        var fromId = new IntentId(parentIntentId);
        var existing = await linkRepository.ListByIntentAsync(fromId, ct);
        var current = new HashSet<string>(StringComparer.Ordinal);
        foreach (var view in existing)
        {
            if (IsSyncChildEdge(view))
            {
                current.Add(view.Other.Id.Value);
            }
        }

        foreach (var childIntentId in desired)
        {
            if (!current.Contains(childIntentId))
            {
                await AddAsync(fromId, new IntentId(childIntentId), ct);
            }
        }

        foreach (var staleChildId in current)
        {
            if (!desired.Contains(staleChildId))
            {
                await unitOfWork.ExecuteAsync(inner => linkRepository.DeleteAsync(fromId, new IntentId(staleChildId), inner), ct);
            }
        }
    }

    private static bool IsSyncChildEdge(IntentLinkView view) =>
        view.Direction == IntentLinkDirection.Outgoing &&
        view.Link.Author == IntentLinkAuthor.Agent &&
        string.Equals(view.Link.Rationale, CardChildRationale, StringComparison.Ordinal);

    private Task<CreateIntentLinkOutcome> AddAsync(IntentId fromId, IntentId toId, CancellationToken ct)
    {
        var link = IntentLink.Create(
            Guid.NewGuid().ToString("N"), fromId, toId, blocking: true, IntentLinkAuthor.Agent, CardChildRationale, clock.GetUtcNow());
        return unitOfWork.ExecuteAsync(inner => linkRepository.CreateAsync(link, inner), ct);
    }
}
