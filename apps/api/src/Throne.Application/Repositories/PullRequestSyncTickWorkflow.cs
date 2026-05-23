using Throne.Application.Ports;

namespace Throne.Application.Repositories;

/// <summary>
/// Per-tick orchestration for the background poller (T-10, ADR-0024 § 6). One
/// <see cref="RunAsync"/> call corresponds to a single tick of the hosted service
/// and visits every binding returned by
/// <see cref="IIntentRepositoryBindingRepository.FindOpenForSyncAsync"/>.
///
/// The actual per-binding decision tree lives in <see cref="PullRequestSyncBindingVisitor"/>
/// so this orchestration stays focused on the loop / cancellation / report-aggregation
/// concerns and inside the per-type CA1502 cyclomatic budget.
/// </summary>
public sealed class PullRequestSyncTickWorkflow(
    IIntentRepositoryBindingRepository bindings,
    PullRequestSyncBindingVisitor visitor)
{
    public async Task<PullRequestSyncTickReport> RunAsync(CancellationToken ct)
    {
        var due = await bindings.FindOpenForSyncAsync(ct);
        var report = new PullRequestSyncTickReport();
        foreach (var binding in due)
        {
            ct.ThrowIfCancellationRequested();
            await visitor.VisitAsync(binding, report, ct);
        }
        return report;
    }
}
