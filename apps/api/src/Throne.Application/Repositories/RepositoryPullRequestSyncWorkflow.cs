using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Sync sub-workflow extracted from <see cref="RepositoryBindingService"/> so the
/// service body stays inside the per-type CA1502 cyclomatic budget. Lives under the
/// Application layer alongside the service; not exposed publicly.
///
/// The workflow honours the contract from parent Q5 / ADR-0024 § 6:
/// <list type="bullet">
///   <item>200 Fresh → record <c>etag</c> + <c>last_synced_at</c>, return comments.</item>
///   <item>304 NotModified → record <c>last_synced_at</c>, keep prior <c>etag</c>, return empty comments + <c>NotModified=true</c>.</item>
///   <item>404 (provider returns null) → <see cref="IntentRepositoryBindingMutator.MarkBroken"/>, throw <c>repository.upstream_gone</c>.</item>
/// </list>
/// </summary>
public sealed class RepositoryPullRequestSyncWorkflow(
    IIntentRepositoryBindingRepository bindings,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<SyncRepositoryPullRequestResult> SyncAsync(
        IntentRepositoryBinding binding,
        IGitProvider provider,
        CancellationToken ct)
    {
        EnsureReady(binding);
        EnsurePullRequestAttached(binding);

        var page = await provider.ListPullRequestCommentsAsync(
            owner: binding.Coordinate.Owner,
            repo: binding.Coordinate.Repo,
            number: binding.State.PullRequestNumber!.Value,
            etag: binding.State.ReviewCommentsEtag,
            ct: ct);

        return await PersistAsync(binding, page, ct);
    }

    private async Task<SyncRepositoryPullRequestResult> PersistAsync(
        IntentRepositoryBinding binding,
        PullRequestCommentsPage? page,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();

        if (page is null)
        {
            binding.MarkBroken("pull request not found upstream (404)", now);
            await SaveAsync(binding, ct);
            throw RepositoryBindingFailures.UpstreamGone(binding);
        }

        var snapshot = ProjectSyncSnapshot(page, binding);
        binding.RecordSync(snapshot.Etag, now);
        var saved = await SaveAsync(binding, ct);
        return new SyncRepositoryPullRequestResult(saved, snapshot.Comments, snapshot.NotModified);
    }

    private async Task<IntentRepositoryBinding> SaveAsync(IntentRepositoryBinding binding, CancellationToken ct)
    {
        var outcome = await unitOfWork.ExecuteAsync(
            inner => bindings.SaveAsync(binding, inner),
            ct);
        return outcome switch
        {
            SaveBindingOutcome.Saved saved => saved.Binding,
            SaveBindingOutcome.NotFound => throw RepositoryBindingFailures.BindingNotFound(
                binding.IntentId.Value, binding.Id.Value),
            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
    }

    private static SyncSnapshot ProjectSyncSnapshot(PullRequestCommentsPage page, IntentRepositoryBinding binding) =>
        page switch
        {
            PullRequestCommentsPage.Fresh fresh => new SyncSnapshot(fresh.Comments, fresh.Etag, NotModified: false),
            PullRequestCommentsPage.NotModified => new SyncSnapshot([], binding.State.ReviewCommentsEtag, NotModified: true),
            _ => throw new InvalidOperationException($"Unhandled page kind: {page.GetType().Name}"),
        };

    private static void EnsureReady(IntentRepositoryBinding binding)
    {
        if (binding.State.CloneStatus != CloneStatusNames.Ready)
        {
            throw RepositoryBindingFailures.BindingNotReady(binding);
        }
    }

    private static void EnsurePullRequestAttached(IntentRepositoryBinding binding)
    {
        if (binding.State.PullRequestNumber is null)
        {
            throw RepositoryBindingFailures.PullRequestNotAttached(binding);
        }
    }

    private sealed record SyncSnapshot(
        IReadOnlyList<PullRequestComment> Comments,
        string? Etag,
        bool NotModified);
}
