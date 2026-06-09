using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Read-only proxy that lists commits of the binding's pull request through the
/// provider. Server is pointer-only.
/// </summary>
public sealed class ListReviewWorkspaceCommitsUseCase(IGitProviderRegistry providers)
{
    public async Task<IReadOnlyList<PullRequestCommitRef>> ListByBindingAsync(
        IntentRepositoryBinding binding,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.State.PullRequestNumber is null)
        {
            throw RepositoryBindingFailures.PullRequestNotAttached(binding);
        }
        var provider = providers.GetByName(binding.Coordinate.Provider)
            ?? throw RepositoryBindingFailures.ProviderUnsupported(binding.Coordinate.Provider);

        var commits = await provider.ListPullRequestCommitsAsync(
            binding.Coordinate.Owner,
            binding.Coordinate.Repo,
            binding.State.PullRequestNumber.Value,
            ct);

        return commits ?? throw RepositoryBindingFailures.UpstreamGone(binding);
    }
}
