using Microsoft.Extensions.Logging;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Auto-bind pass (intent spec A). One <see cref="RunAsync"/> call is a single tick: it scans
/// every <c>ready</c> binding with no PR attached, reads the local clone's current branch, and
/// links the open PR whose head matches — but only when there is <b>exactly one</b> match
/// (0 or &gt;1 ⇒ left for manual binding). This fills the empty PR slot without touching the
/// workspace, removing the old «delete the binding, rebind with a PR number» dance.
///
/// Runs in the same hosted service as the comment-sync tick
/// (<see cref="PullRequestSyncTickWorkflow"/>) but as a separate pass over a separate query
/// (<see cref="IIntentRepositoryBindingRepository.FindReadyWithoutPullRequestAsync"/>).
/// </summary>
public sealed partial class PullRequestAutoBindWorkflow(
    IIntentRepositoryBindingRepository bindings,
    IGitProviderRegistry providers,
    ILocalGitBranchReader branchReader,
    RepositoryBindingPersistence persistence,
    IWorkspaceRootProvider workspace,
    ILogger<PullRequestAutoBindWorkflow> logger)
{
    // Open-PR page size for the head match. gh's default is 30; a generous cap keeps the
    // agent's just-opened PR in view even on a repo with many open PRs.
    private const int OpenPrScanLimit = 100;


    public async Task<PullRequestAutoBindReport> RunAsync(CancellationToken ct)
    {
        var due = await bindings.FindReadyWithoutPullRequestAsync(ct);
        var report = new PullRequestAutoBindReport();
        foreach (var binding in due)
        {
            ct.ThrowIfCancellationRequested();
            await TryBindAsync(binding, report, ct);
        }
        return report;
    }

    /// <summary>
    /// On-demand single-binding pass for the «Обновить» button (ADR-0024): same per-binding
    /// logic as <see cref="RunAsync"/>, but for an already-loaded binding instead of the
    /// scheduled scan. Outcome is folded into the returned report; exceptions are swallowed
    /// into <see cref="PullRequestAutoBindReport.Failed"/> so a transient provider/network
    /// hiccup never breaks the refresh response.
    /// </summary>
    public async Task<PullRequestAutoBindReport> RunForAsync(
        IntentRepositoryBinding binding, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);
        var report = new PullRequestAutoBindReport();
        await TryBindAsync(binding, report, ct);
        return report;
    }

    private async Task TryBindAsync(
        IntentRepositoryBinding binding, PullRequestAutoBindReport report, CancellationToken ct)
    {
        try
        {
            await BindAsync(binding, report, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogBindFailed(logger, binding.Id.Value, ex);
            report.Failed++;
        }
    }

    private async Task BindAsync(
        IntentRepositoryBinding binding, PullRequestAutoBindReport report, CancellationToken ct)
    {
        var provider = providers.GetByName(binding.Coordinate.Provider);
        if (provider is null)
        {
            LogProviderMissing(logger, binding.Id.Value, binding.Coordinate.Provider);
            report.Failed++;
            return;
        }

        // Recompute against the live root, not binding.WorkspacePath: the persisted path
        // embeds the root in effect at clone-time and goes stale across machines (shared
        // persistence) or a runtime model switch (ADR-0027) — same reasoning as DeleteAsync /
        // RepositoryCloneWorkflow. Reading the branch via the stale path leaves the «Обновить»
        // button silently unable to bind an open PR.
        var workspacePath = WorkspacePathLayout.Compute(
            workspace.ResolvedRoot, binding.IntentId, binding.Coordinate);
        var branch = await branchReader.ReadCurrentBranchAsync(workspacePath, ct);
        if (branch is null)
        {
            LogNoBranch(logger, binding.Id.Value, workspacePath);
            report.Skipped++;
            return;
        }

        var prs = await provider.ListPullRequestsAsync(
            binding.Coordinate.Owner, binding.Coordinate.Repo, query: branch, OpenPrScanLimit, ct);
        var matches = prs.Where(p => string.Equals(p.HeadRef, branch, StringComparison.Ordinal)).ToList();
        if (matches.Count == 0)
        {
            LogNoMatch(logger, binding.Id.Value, branch, prs.Count);
            report.Skipped++;
            return;
        }
        if (matches.Count > 1)
        {
            LogMultipleMatches(logger, binding.Id.Value, branch, matches.Count);
            report.Skipped++;
            return;
        }

        await persistence.AttachPullRequestAsync(binding, matches[0].Number, ct);
        LogAttached(logger, binding.Id.Value, matches[0].Number, branch);
        report.Bound++;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "PullRequestAutoBindWorkflow: binding {BindingId} skipped — git provider {Provider} not registered.")]
    private static partial void LogProviderMissing(ILogger logger, string bindingId, string provider);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "PullRequestAutoBindWorkflow: binding {BindingId} skipped — could not read current branch of local clone at {WorkspacePath}.")]
    private static partial void LogNoBranch(ILogger logger, string bindingId, string workspacePath);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "PullRequestAutoBindWorkflow: binding {BindingId} skipped — no open PR with head=={Branch} (scanned={Scanned}).")]
    private static partial void LogNoMatch(ILogger logger, string bindingId, string branch, int scanned);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information,
        Message = "PullRequestAutoBindWorkflow: binding {BindingId} skipped — {MatchCount} open PRs share head=={Branch}, manual bind required.")]
    private static partial void LogMultipleMatches(ILogger logger, string bindingId, string branch, int matchCount);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information,
        Message = "PullRequestAutoBindWorkflow: binding {BindingId} attached PR #{Pr} via head=={Branch}.")]
    private static partial void LogAttached(ILogger logger, string bindingId, int pr, string branch);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error,
        Message = "PullRequestAutoBindWorkflow: binding {BindingId} bind attempt threw.")]
    private static partial void LogBindFailed(ILogger logger, string bindingId, Exception exception);
}

/// <summary>Per-tick counters for the auto-bind pass. Consumed by the host's structured log.</summary>
public sealed class PullRequestAutoBindReport
{
    public int Bound { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
}
