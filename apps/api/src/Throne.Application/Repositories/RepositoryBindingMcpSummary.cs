using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Compact projection of <see cref="IntentRepositoryBinding"/> for the MCP
/// <c>get_intent.repositories[]</c> field (T-13). Mirrors the
/// <c>RepositoryBindingSummary</c> contract DTO from
/// <c>specs/contracts/repositories/openapi.yaml</c> — wire fields are returned in
/// snake_case strings (ADR-0023) so the MCP renderer can serialise without further
/// mapping. Internal fields (<c>etag</c>, <c>last_synced_at</c>, <c>clone_error</c>)
/// are intentionally hidden per the slice-1 read-only design.
/// </summary>
public sealed record RepositoryBindingMcpSummary(
    string BindingId,
    string Provider,
    string Owner,
    string Repo,
    string DefaultBranch,
    string WorkspacePath,
    string CloneStatus,
    int? PullRequestNumber,
    string? PullRequestState);

/// <summary>
/// Pure mapper <see cref="IntentRepositoryBinding"/> → <see cref="RepositoryBindingMcpSummary"/>.
/// Lives in Application so it can be unit-tested without taking the MCP transport
/// host. The mapper is intentionally lossy — internal fields stay inside the
/// domain aggregate; full projection is reserved for the HTTP module (T-11).
/// </summary>
public static class RepositoryBindingMcpSummaryMapper
{
    public static RepositoryBindingMcpSummary ToSummary(IntentRepositoryBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        var state = binding.State;
        return new RepositoryBindingMcpSummary(
            BindingId: binding.Id.Value,
            Provider: binding.Coordinate.Provider,
            Owner: binding.Coordinate.Owner,
            Repo: binding.Coordinate.Repo,
            DefaultBranch: state.DefaultBranch,
            WorkspacePath: binding.WorkspacePath,
            CloneStatus: state.CloneStatus,
            PullRequestNumber: state.PullRequestNumber,
            PullRequestState: state.PullRequestState);
    }

    public static IReadOnlyList<RepositoryBindingMcpSummary> ToSummaries(
        IReadOnlyList<IntentRepositoryBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        if (bindings.Count == 0)
        {
            return [];
        }
        var list = new List<RepositoryBindingMcpSummary>(bindings.Count);
        foreach (var b in bindings)
        {
            list.Add(ToSummary(b));
        }
        return list;
    }
}
