using Throne.Application.Ports;

namespace Throne.Application.Repositories;

/// <summary>
/// Per-tick orchestration for the background poller (ADR-0024 § 6). One
/// <see cref="RunAsync"/> call corresponds to a single tick of the hosted service
/// and visits every binding returned by
/// <see cref="IIntentRepositoryBindingRepository.FindOpenForSyncAsync"/>.
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
