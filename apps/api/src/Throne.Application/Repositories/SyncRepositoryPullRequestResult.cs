using Throne.Application.Events;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Result of <see cref="RepositoryPullRequestSyncWorkflow.SyncAsync"/>. Carries the
/// refreshed binding (with bumped <c>last_synced_at</c>), the full stored review-comment
/// feed for the binding after the sync, and the subset of comments that were observed
/// for the first time on this pass — those drive the <c>intent.pr_comment_added</c>
/// fanout via the dispatching unit-of-work.
///
/// <see cref="NotModified"/> is <see langword="true"/> when the upstream returned 304 —
/// in that case <see cref="NewComments"/> is empty, no per-comment events are raised,
/// and <see cref="AllStored"/> reflects the previously cached feed unchanged.
///
/// The same carrier is returned by both the manual sync use-case (T-08
/// <c>RepositoryBindingService.SyncPullRequestAsync</c>) and the background poller
/// (T-10 <c>PullRequestSyncService</c>), so the persistence + fanout pipeline is
/// shared and the two paths cannot drift apart.
/// </summary>
public sealed record SyncRepositoryPullRequestResult(
    IntentRepositoryBinding Binding,
    IReadOnlyList<PullRequestCommentRecord> NewComments,
    IReadOnlyList<PullRequestCommentRecord> AllStored,
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
