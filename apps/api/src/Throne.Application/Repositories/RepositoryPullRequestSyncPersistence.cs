using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Persistence-side helper for <see cref="RepositoryPullRequestSyncWorkflow"/>. Owns
/// the «save binding state + store new comments» transaction and the page-kind
/// switch — keeping the workflow itself focused on the upstream call shape and the
/// 404 → broken decision so the per-type CA1502 cyclomatic budget holds.
/// </summary>
public sealed class RepositoryPullRequestSyncPersistence(
    IIntentRepositoryBindingRepository bindings,
    IPullRequestCommentRepository comments,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public TimeProvider Clock => clock;

    public Task<SyncRepositoryPullRequestResult> PersistFreshAsync(
        IntentRepositoryBinding binding,
        PullRequestCommentsPage page,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(page);
        var now = clock.GetUtcNow();
        var snapshot = ProjectIncoming(page, binding);
        var records = MapToRecords(snapshot.Comments, binding, now);

        // Wrap save + comment-persist in a single unit of work and return the public
        // SyncRepositoryPullRequestResult so the dispatching unit-of-work decorator
        // sees one carrier with both RepositoryPullRequestSynced and the per-comment
        // IntentPrCommentAdded events. Without this the events would stop at the
        // inner PersistPullRequestCommentsOutcome and the outer carrier's events
        // would never reach the realtime fanout (T-12).
        return unitOfWork.ExecuteAsync(
            async inner =>
            {
                var savedBinding = await SaveBindingStateAsync(binding, snapshot.Etag, now, inner);
                var outcome = await comments.PersistNewAsync(savedBinding, records, inner);
                return new SyncRepositoryPullRequestResult(
                    Binding: outcome.Binding,
                    NewComments: outcome.Inserted,
                    AllStored: outcome.AllStored,
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
        DateTimeOffset now,
        CancellationToken ct)
    {
        binding.RecordSync(etag, now);
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

    private static List<PullRequestCommentRecord> MapToRecords(
        IReadOnlyList<PullRequestComment> fetched,
        IntentRepositoryBinding binding,
        DateTimeOffset observedAt) =>
        fetched
            .Select(c => new PullRequestCommentRecord(
                BindingId: binding.Id,
                IntentId: binding.IntentId,
                UpstreamId: c.Id,
                AuthorLogin: c.AuthorLogin,
                Body: c.Body,
                CreatedAt: c.CreatedAt,
                ObservedAt: observedAt,
                AuthorAvatarUrl: c.AuthorAvatarUrl,
                HtmlUrl: c.HtmlUrl,
                Path: c.Path,
                UpdatedAt: c.UpdatedAt))
            .ToList();

    private sealed record IncomingPage(
        IReadOnlyList<PullRequestComment> Comments,
        string? Etag,
        bool NotModified);
}
