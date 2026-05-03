using Throne.Application.Ports;

namespace Throne.Application.DreamRuns;

public sealed record GetDreamReadinessQuery;

public sealed class GetDreamReadinessHandler(
    IDreamRunRepository runs,
    DreamWindowResolver windows)
{
    public async Task<ReadinessSnapshot> HandleAsync(GetDreamReadinessQuery _, CancellationToken ct)
    {
        var assembly = await windows.AssembleAsync(ct);
        var pendingRuns = await runs.ListPendingAsync(ct);
        var pendingProposals = pendingRuns.Sum(r => r.PendingCount);
        return ReadinessProjector.Project(assembly, pendingProposals, pendingRuns.Count);
    }
}
