using Throne.Application.Ports;

namespace Throne.Application.DreamRuns;

public sealed record GetDreamReadinessQuery;

public sealed class GetDreamReadinessHandler(
    IDreamRunRepository runs,
    DreamWindowResolver windows,
    DreamOptions options)
{
    private readonly DreamOptions _options = options;

    public async Task<ReadinessSnapshot> HandleAsync(GetDreamReadinessQuery _, CancellationToken ct)
    {
        var assembly = await windows.AssembleAsync(ct);
        var pendingRuns = await runs.ListPendingAsync(ct);
        var pendingProposals = pendingRuns.Sum(r => r.PendingCount);

        var calculator = new ReadinessCalculator(_options);
        return calculator.Calculate(
            assembly.Window,
            pendingProposals,
            pendingRuns.Count,
            assembly.LockedScore);
    }
}
