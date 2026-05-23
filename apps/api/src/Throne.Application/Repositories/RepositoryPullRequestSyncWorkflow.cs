using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Sync sub-workflow extracted from <see cref="RepositoryBindingService"/> so the
/// service body stays inside the per-type CA1502 cyclomatic budget. Lives under the
/// Application layer alongside the service; not exposed publicly.
///
/// The workflow honours the contract from parent Q5 / ADR-0024 § 6 — and is shared
/// between the manual sync use-case (T-08) and the background poller (T-10) so the
/// «store new comments → fan out <c>intent.pr_comment_added</c>» pipeline cannot
/// drift between the two surfaces:
/// <list type="bullet">
///   <item>200 Fresh → persist new comments (idempotent by upstream id), record
///         <c>etag</c> + <c>last_synced_at</c> on the binding, return the
///         freshly-inserted records plus the full stored feed.</item>
///   <item>304 NotModified → record <c>last_synced_at</c>, keep prior <c>etag</c>,
///         no new comments and no per-comment events; the result still echoes the
///         previously-stored feed so the caller can render it unchanged.</item>
///   <item>404 (provider returns null) → <see cref="IntentRepositoryBindingMutator.MarkBroken"/>,
///         throw <c>repository.upstream_gone</c>.</item>
/// </list>
///
/// Persistence (save binding + insert comments + outcome carriers) is delegated to
/// <see cref="RepositoryPullRequestSyncPersistence"/> so the workflow body itself stays
/// inside the per-type cyclomatic budget.
/// </summary>
public sealed class RepositoryPullRequestSyncWorkflow(RepositoryPullRequestSyncPersistence persistence)
{
    public async Task<SyncRepositoryPullRequestResult> SyncAsync(
        IntentRepositoryBinding binding,
        IGitProvider provider,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(provider);
        EnsureReady(binding);
        EnsurePullRequestAttached(binding);

        var page = await provider.ListPullRequestCommentsAsync(
            owner: binding.Coordinate.Owner,
            repo: binding.Coordinate.Repo,
            number: binding.State.PullRequestNumber!.Value,
            etag: binding.State.ReviewCommentsEtag,
            ct: ct);

        if (page is null)
        {
            await persistence.MarkBrokenAsync(binding, "pull request not found upstream (404)", ct);
            throw RepositoryBindingFailures.UpstreamGone(binding);
        }

        return await persistence.PersistFreshAsync(binding, page, ct);
    }

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
}
