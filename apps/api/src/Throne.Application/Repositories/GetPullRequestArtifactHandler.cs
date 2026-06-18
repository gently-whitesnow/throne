using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

public sealed record GetPullRequestArtifactQuery(BindingId BindingId, string Type);

public sealed class GetPullRequestArtifactHandler(IPullRequestArtifactRepository artifacts)
{
    public async Task<PullRequestArtifact> HandleAsync(GetPullRequestArtifactQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await artifacts.GetAsync(query.BindingId, query.Type, ct)
            ?? throw new ApiException(
                ErrorCodes.PullRequestArtifactNotFound,
                $"Pull request artifact not found: {query.BindingId.Value}/{query.Type}.",
                new Dictionary<string, object?>
                {
                    ["binding_id"] = query.BindingId.Value,
                    ["type"] = query.Type,
                });
    }
}
