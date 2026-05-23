using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Per-scope <c>gh</c> repo enumeration. <c>mine</c> goes through
/// <c>gh repo list --json</c>; <c>involved</c> overlays
/// <c>gh api /user/repos?affiliation=collaborator,organization_member</c> per
/// ADR-0024 § 3 and is deduplicated by <c>{owner}/{repo}</c>. The two CLI
/// calls live in <see cref="GhRepoListExecutor"/> to keep this class inside the
/// CA1502 cyclomatic budget.
/// </summary>
internal sealed class GhRepoSearcher(GhCliInvoker gh, GhRepoListExecutor executor)
{
    public async Task<IReadOnlyList<GitRepositoryRef>> SearchAsync(
        RepositorySearchScope scope,
        string? query,
        int limit,
        CancellationToken ct)
    {
        var effectiveLimit = limit > 0 ? limit : gh.PageSize;
        var mine = await executor.MineAsync(effectiveLimit, ct);
        var combined = await CombineForScopeAsync(scope, mine, effectiveLimit, ct);
        return RepoSearchFilter.Apply(combined, query, effectiveLimit);
    }

    private async Task<IReadOnlyList<GitRepositoryRef>> CombineForScopeAsync(
        RepositorySearchScope scope,
        IReadOnlyList<GitRepositoryRef> mine,
        int limit,
        CancellationToken ct)
    {
        if (scope == RepositorySearchScope.Mine)
        {
            return mine;
        }

        if (scope == RepositorySearchScope.Involved)
        {
            var involved = await executor.InvolvedAsync(limit, ct);
            return GhRepoMerger.Merge(mine, involved);
        }

        throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown search scope.");
    }
}
