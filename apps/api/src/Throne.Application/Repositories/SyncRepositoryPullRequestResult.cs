using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Result of <see cref="RepositoryBindingService.SyncPullRequestAsync"/>. Carries the
/// refreshed binding (with bumped <c>last_synced_at</c>), the comments returned by the
/// provider for the active sync, and a domain event so the dispatching unit-of-work
/// fans <see cref="RepositoryPullRequestSynced"/> out without the service touching
/// realtime emitters directly. Per-comment <c>intent.pr_comment_added</c> fanout is
/// owned by the background poller (T-10) which holds the comment store.
///
/// <see cref="NotModified"/> is <see langword="true"/> when the upstream returned 304 —
/// in that case <see cref="Comments"/> is empty and the caller decides whether to read
/// previously stored comments from T-10's collection.
/// </summary>
public sealed record SyncRepositoryPullRequestResult(
    IntentRepositoryBinding Binding,
    IReadOnlyList<PullRequestComment> Comments,
    bool NotModified) : IDomainEventCarrier
{
    public IReadOnlyList<IDomainEvent> Events =>
        [new RepositoryPullRequestSynced(Binding, Comments.Count)];
}
