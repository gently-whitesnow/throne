using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

public sealed record ListPullRequestArtifactsQuery(BindingId BindingId);

public sealed class ListPullRequestArtifactsHandler(IPullRequestArtifactRepository artifacts)
{
    public Task<IReadOnlyList<PullRequestArtifact>> HandleAsync(
        ListPullRequestArtifactsQuery query,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        return artifacts.ListAsync(query.BindingId, ct);
    }
}
