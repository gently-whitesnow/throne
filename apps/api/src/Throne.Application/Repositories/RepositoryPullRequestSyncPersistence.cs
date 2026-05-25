using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Persistence-side helper for <see cref="RepositoryPullRequestSyncWorkflow"/>. Owns
/// the binding-state save and the page-kind switch.
///
/// Per intent 9aa2c64ff2a94410b7352eada1350ad0 the server keeps a pointer-only cursor
/// (<c>review_comments_etag</c> + <c>last_seen_review_comment_at</c>) and never persists
/// comment bodies. New comments are detected by filtering the upstream-fresh page against
/// the stored cursor; the carrier returned here fans <see cref="Events.IntentPrCommentAdded"/>
/// out with the full body still in memory.
/// </summary>
public sealed class RepositoryPullRequestSyncPersistence(
    IIntentRepositoryBindingRepository bindings,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public TimeProvider Clock => clock;

    public Task<SyncRepositoryPullRequestResult> PersistFreshAsync(
        IntentRepositoryBinding binding,
        PullRequestCommentsPage page,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(page);

        var snapshot = ProjectIncoming(page, binding);
        var newComments = PullRequestCommentCursor.FilterNew(snapshot.Comments, binding.State.LastSeenReviewCommentAt);
        var cursor = PullRequestCommentCursor.Advance(binding.State.LastSeenReviewCommentAt, newComments);
        var now = clock.GetUtcNow();

        // Wrap binding save in the unit of work so the dispatching decorator sees the
        // SyncRepositoryPullRequestResult carrier and fans out the per-comment events
        // after the binding-state write commits.
        return unitOfWork.ExecuteAsync(
            async inner =>
            {
                var savedBinding = await SaveBindingStateAsync(binding, snapshot.Etag, cursor, now, inner);
                return new SyncRepositoryPullRequestResult(
                    Binding: savedBinding,
                    NewComments: newComments,
                    AllStored: snapshot.Comments,
                    NotModified: snapshot.NotModified);
            },
            ct);
    }

    public async Task MarkBrokenAsync(
        IntentRepositoryBinding binding,
        string reason,
        CancellationToken ct)
    {
        binding.MarkBroken(reason, clock.GetUtcNow());
        var outcome = await unitOfWork.ExecuteAsync(
            inner => bindings.SaveAsync(binding, inner),
            ct);
        if (outcome is SaveBindingOutcome.NotFound)
        {
            throw RepositoryBindingFailures.BindingNotFound(
                binding.IntentId.Value, binding.Id.Value);
        }
    }

    private async Task<IntentRepositoryBinding> SaveBindingStateAsync(
        IntentRepositoryBinding binding,
        string? etag,
        DateTimeOffset? cursor,
        DateTimeOffset now,
        CancellationToken ct)
    {
        binding.RecordSync(etag, cursor, now);
        var outcome = await bindings.SaveAsync(binding, ct);
        return outcome switch
        {
            SaveBindingOutcome.Saved saved => saved.Binding,
            SaveBindingOutcome.NotFound => throw RepositoryBindingFailures.BindingNotFound(
                binding.IntentId.Value, binding.Id.Value),
            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
    }

    private static IncomingPage ProjectIncoming(
        PullRequestCommentsPage page,
        IntentRepositoryBinding binding) =>
        page switch
        {
            PullRequestCommentsPage.Fresh fresh => new IncomingPage(fresh.Comments, fresh.Etag, NotModified: false),
            PullRequestCommentsPage.NotModified => new IncomingPage([], binding.State.ReviewCommentsEtag, NotModified: true),
            _ => throw new InvalidOperationException($"Unhandled page kind: {page.GetType().Name}"),
        };

    private sealed record IncomingPage(
        IReadOnlyList<PullRequestComment> Comments,
        string? Etag,
        bool NotModified);
}
