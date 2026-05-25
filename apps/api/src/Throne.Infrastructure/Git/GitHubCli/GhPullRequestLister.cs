using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Lists open pull requests of a GitHub repository via <c>gh pr list --state open</c>.
/// Only <c>state=open</c> is in scope for slice 1 of the bind-modal typeahead;
/// closed and merged PRs are intentionally excluded (see intent postановка).
/// </summary>
internal sealed class GhPullRequestLister(GhCliInvoker gh)
{
    public async Task<IReadOnlyList<GitPullRequestRef>> ListAsync(
        string owner,
        string repo,
        string? query,
        int limit,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repo);

        var effectiveLimit = limit > 0 ? limit : gh.PageSize;
        var result = await gh.RunAsync(GhPullRequestListArgs.OpenList(owner, repo, effectiveLimit), ct);
        if (!result.IsSuccess)
        {
            throw GhExceptions.FromExit($"pr list -R {owner}/{repo}", result);
        }

        var prs = GhPullRequestListParser.Parse(result.StandardOutput);
        return GhPullRequestFilter.Apply(prs, query, effectiveLimit);
    }
}
