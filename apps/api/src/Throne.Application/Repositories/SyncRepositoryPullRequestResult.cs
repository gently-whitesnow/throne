using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Result of <see cref="RepositoryPullRequestSyncWorkflow.SyncAsync"/>. Carries the
/// refreshed binding (with bumped <c>last_synced_at</c> + <c>last_seen_review_comment_at</c>),
/// the upstream-fresh review-comment feed for the binding observed on this pass, and the
/// subset of comments that were seen for the first time — those drive the
/// <c>intent.pr_comment_added</c> fanout via the dispatching unit-of-work.
///
/// <see cref="NotModified"/> is <see langword="true"/> when the upstream returned 304 —
/// in that case <see cref="NewComments"/> and <see cref="AllStored"/> are empty, no
/// per-comment events are raised, and HTTP callers reuse their client-side cache.
///
/// The server never persists comment bodies: <see cref="AllStored"/> is the
/// upstream-fresh list from the provider call, not a persistence projection. The
/// name is preserved for wire-shape continuity.
///
/// The same carrier is returned by both the manual sync use-case
/// (<c>RepositoryBindingService.SyncPullRequestAsync</c>) and the background poller
/// (<c>PullRequestSyncService</c>), so the persistence + fanout pipeline is shared and
/// the two paths cannot drift apart.
/// </summary>
public sealed record SyncRepositoryPullRequestResult(
    IntentRepositoryBinding Binding,
    IReadOnlyList<PullRequestComment> NewComments,
    IReadOnlyList<PullRequestComment> AllStored,
    bool NotModified) : IDomainEventCarrier
{
    public IReadOnlyList<IDomainEvent> Events
    {
        get
        {
            var events = new List<IDomainEvent>(NewComments.Count + 1)
            {
                new RepositoryPullRequestSynced(Binding, NewComments.Count),
            };
            events.AddRange(NewComments.Select(c => (IDomainEvent)new IntentPrCommentAdded(Binding, c)));
            return events;
        }
    }
}
