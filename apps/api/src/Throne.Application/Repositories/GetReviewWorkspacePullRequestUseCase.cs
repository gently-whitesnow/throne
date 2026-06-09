using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Read-only proxy that fetches the binding's pull-request header (title, state,
/// author, branches, description) from the provider. Server is pointer-only:
/// never persists the body — same read-through contract as the diff endpoint.
/// </summary>
public sealed class GetReviewWorkspacePullRequestUseCase(IGitProviderRegistry providers)
{
    public async Task<PullRequestSnapshot> GetAsync(IntentRepositoryBinding binding, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.State.PullRequestNumber is null)
        {
            throw RepositoryBindingFailures.PullRequestNotAttached(binding);
        }
        var provider = providers.GetByName(binding.Coordinate.Provider)
            ?? throw RepositoryBindingFailures.ProviderUnsupported(binding.Coordinate.Provider);

        var snapshot = await provider.GetPullRequestAsync(
            binding.Coordinate.Owner,
            binding.Coordinate.Repo,
            binding.State.PullRequestNumber.Value,
            ct);

        return snapshot ?? throw RepositoryBindingFailures.UpstreamGone(binding);
    }
}
