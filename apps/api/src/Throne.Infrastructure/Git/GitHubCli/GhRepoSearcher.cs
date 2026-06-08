using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Per-scope <c>gh</c> repo enumeration. <c>mine</c> goes through
/// <c>gh repo list --json</c>; <c>involved</c> overlays
/// <c>gh api /user/repos?affiliation=collaborator,organization_member</c> per
/// ADR-0024 § 3 and is deduplicated by <c>{owner}/{repo}</c>.
/// </summary>
internal sealed class GhRepoSearcher(GhCliInvoker gh, GhRepoListExecutor executor)
{
    public async Task<IReadOnlyList<GitRepositoryRef>> SearchAsync(
        RepositorySearchScope scope,
        string? query,
        int limit,
        CancellationToken ct)
    {
        var displayLimit = limit > 0 ? limit : gh.PageSize;
        // Empty query just browses the recent window; a query filters client-side
        // (RepoSearchFilter), so the fetch must cover the whole account or older
        // repos never reach the filter and look "missing" in autocomplete.
        var fetchLimit = string.IsNullOrWhiteSpace(query) ? displayLimit : gh.SearchFetchLimit;
        var mine = await executor.MineAsync(fetchLimit, ct);
        var combined = await CombineForScopeAsync(scope, mine, fetchLimit, ct);
        return RepoSearchFilter.Apply(combined, query, displayLimit);
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
