using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Sync sub-workflow shared between the manual sync use-case and the background
/// poller so the «detect new comments → fan out <c>intent.pr_comment_added</c>»
/// pipeline cannot drift between the two surfaces.
///
/// The server is pointer-only: it stores the <c>review_comments_etag</c> +
/// <c>last_seen_review_comment_at</c> cursor on the binding and never persists
/// comment bodies.
/// <list type="bullet">
///   <item>200 Fresh → filter incoming comments against the cursor, raise
///         <c>IntentPrCommentAdded</c> per new comment with the full body in the
///         payload, advance <c>etag</c> + cursor + <c>last_synced_at</c>, and return
///         the upstream-fresh feed as the result.</item>
///   <item>304 NotModified → record <c>last_synced_at</c>, keep prior cursor and
///         etag, no new comments and no per-comment events; HTTP callers reuse their
///         client-side cache.</item>
///   <item>404 (provider returns null) → <see cref="IntentRepositoryBinding.MarkBroken"/>,
///         throw <c>repository.upstream_gone</c>.</item>
/// </list>
///
/// Persistence (save binding + outcome carriers) is delegated to
/// <see cref="RepositoryPullRequestSyncPersistence"/> so the workflow body itself stays
/// inside the per-type cyclomatic budget.
/// </summary>
public sealed class RepositoryPullRequestSyncWorkflow(
    RepositoryPullRequestSyncPersistence persistence,
    PullRequestStateRefresher stateRefresher)
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

    /// <summary>
    /// Review D2 — manual sync (POST .../sync) variant that refreshes
    /// <c>pull_request_state</c> before fetching comments. The background poller
    /// (<see cref="PullRequestSyncBindingVisitor"/>) already calls
    /// <see cref="PullRequestStateRefresher.RefreshAsync"/> itself before invoking
    /// <see cref="SyncAsync"/>, so the inner pipeline must stay refresh-free to
    /// avoid double round-trips per tick. Manual sync, on the other hand, used to
    /// skip the refresh entirely — leaving a binding with stale <c>open</c> state
    /// when the PR was closed upstream until the next polling tick.
    /// </summary>
    public async Task<SyncRepositoryPullRequestResult> RefreshAndSyncAsync(
        IntentRepositoryBinding binding,
        IGitProvider provider,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(provider);
        EnsureReady(binding);
        EnsurePullRequestAttached(binding);

        var refreshed = await stateRefresher.RefreshAsync(binding, provider, ct);
        if (refreshed is null)
        {
            await persistence.MarkBrokenAsync(binding, "pull request not found upstream (404)", ct);
            throw RepositoryBindingFailures.UpstreamGone(binding);
        }

        return await SyncAsync(refreshed, provider, ct);
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
