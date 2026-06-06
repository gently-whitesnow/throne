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
public sealed class PullRequestAutoBindWorkflow(
    IIntentRepositoryBindingRepository bindings,
    IGitProviderRegistry providers,
    ILocalGitBranchReader branchReader,
    RepositoryBindingPersistence persistence)
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
        catch (Exception)
        {
            report.Failed++;
        }
    }

    private async Task BindAsync(
        IntentRepositoryBinding binding, PullRequestAutoBindReport report, CancellationToken ct)
    {
        var provider = providers.GetByName(binding.Coordinate.Provider);
        if (provider is null)
        {
            report.Failed++;
            return;
        }

        var branch = await branchReader.ReadCurrentBranchAsync(binding.WorkspacePath, ct);
        if (branch is null)
        {
            report.Skipped++;
            return;
        }

        var prs = await provider.ListPullRequestsAsync(
            binding.Coordinate.Owner, binding.Coordinate.Repo, query: branch, OpenPrScanLimit, ct);
        var matches = prs.Where(p => string.Equals(p.HeadRef, branch, StringComparison.Ordinal)).ToList();
        if (matches.Count != 1)
        {
            report.Skipped++;
            return;
        }

        await persistence.AttachPullRequestAsync(binding, matches[0].Number, ct);
        report.Bound++;
    }
}

/// <summary>Per-tick counters for the auto-bind pass. Consumed by the host's structured log.</summary>
public sealed class PullRequestAutoBindReport
{
    public int Bound { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
}
