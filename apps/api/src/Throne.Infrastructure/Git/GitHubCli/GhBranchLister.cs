using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Lists branches of a GitHub repository via two parallel <c>gh api</c> calls:
/// one to <c>/repos/{owner}/{repo}</c> for the upstream default branch and one
/// to <c>/repos/{owner}/{repo}/branches</c> for the page itself. Splitting args /
/// parser / filter into dedicated helpers keeps this façade inside the CA1502
/// cyclomatic budget — mirrors the <see cref="GhRepoSearcher"/> structure.
/// </summary>
internal sealed class GhBranchLister(GhCliInvoker gh)
{
    public async Task<IReadOnlyList<GitBranchRef>> ListAsync(
        string owner,
        string repo,
        string? query,
        int limit,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repo);

        var effectiveLimit = limit > 0 ? limit : gh.PageSize;

        var defaultTask = gh.RunAsync(GhBranchListArgs.RepoView(owner, repo), ct);
        var branchesTask = gh.RunAsync(GhBranchListArgs.Branches(owner, repo, effectiveLimit), ct);
        await Task.WhenAll(defaultTask, branchesTask);

        var defaultResult = await defaultTask;
        if (!defaultResult.IsSuccess)
        {
            throw GhExceptions.FromExit($"api repos/{owner}/{repo}", defaultResult);
        }

        var branchResult = await branchesTask;
        if (!branchResult.IsSuccess)
        {
            throw GhExceptions.FromExit($"api repos/{owner}/{repo}/branches", branchResult);
        }

        var defaultBranch = GhBranchListParser.ParseDefault(defaultResult.StandardOutput);
        var branches = GhBranchListParser.ParseBranches(branchResult.StandardOutput, defaultBranch);
        return GhBranchFilter.Apply(branches, query, effectiveLimit);
    }
}
