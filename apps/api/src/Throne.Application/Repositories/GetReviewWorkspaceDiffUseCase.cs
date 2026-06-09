using Throne.Application.Errors;
using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Read-only proxy that fetches a provider-derived diff (request or commit scope)
/// for the binding's pull request. Server is pointer-only: never persists the diff.
/// </summary>
public sealed class GetReviewWorkspaceDiffUseCase(IGitProviderRegistry providers)
{
    public Task<PullRequestDiff> GetRequestDiffAsync(IntentRepositoryBinding binding, CancellationToken ct) =>
        GetAsync(binding, scope: ReviewDiffScope.Request, commitSha: null, ct);

    public Task<PullRequestDiff> GetCommitDiffAsync(IntentRepositoryBinding binding, string commitSha, CancellationToken ct) =>
        GetAsync(binding, scope: ReviewDiffScope.Commit, commitSha, ct);

    private async Task<PullRequestDiff> GetAsync(
        IntentRepositoryBinding binding,
        ReviewDiffScope scope,
        string? commitSha,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.State.PullRequestNumber is null)
        {
            throw RepositoryBindingFailures.PullRequestNotAttached(binding);
        }
        var provider = providers.GetByName(binding.Coordinate.Provider)
            ?? throw RepositoryBindingFailures.ProviderUnsupported(binding.Coordinate.Provider);

        var diff = scope == ReviewDiffScope.Request
            ? await provider.GetPullRequestDiffAsync(
                binding.Coordinate.Owner,
                binding.Coordinate.Repo,
                binding.State.PullRequestNumber.Value,
                ct)
            : await provider.GetCommitDiffAsync(
                binding.Coordinate.Owner,
                binding.Coordinate.Repo,
                commitSha ?? throw new ArgumentException("commit_sha is required for scope=commit.", nameof(commitSha)),
                ct);

        return diff ?? throw RepositoryBindingFailures.UpstreamGone(binding);
    }
}

public enum ReviewDiffScope
{
    Request = 0,
    Commit = 1,
}
