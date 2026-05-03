using Throne.Application.Ports;
using Throne.Domain.DreamRuns;

namespace Throne.Application.DreamRuns;

public sealed record ListPendingDreamRunsQuery;

public sealed class ListPendingDreamRunsHandler(IDreamRunRepository runs)
{
    public Task<IReadOnlyList<DreamRun>> HandleAsync(ListPendingDreamRunsQuery _, CancellationToken ct) =>
        runs.ListPendingAsync(ct);
}

public sealed record GetPendingProposalsCountQuery;

public sealed class GetPendingProposalsCountHandler(IDreamRunRepository runs)
{
    public Task<int> HandleAsync(GetPendingProposalsCountQuery _, CancellationToken ct) =>
        runs.GetPendingProposalsCountAsync(ct);
}
