using System.Globalization;
using Throne.Application.Git;
using Throne.Infrastructure.Git.GitHubCli;

namespace Throne.Infrastructure.Git.GitLabCli;

internal sealed class GlabBranchLister(GlabCliInvoker glab, IGitLabHostProvider hostProvider)
{
    public async Task<IReadOnlyList<GitBranchRef>> ListAsync(
        string owner,
        string repo,
        string? query,
        int limit,
        CancellationToken ct)
    {
        var host = await hostProvider.GetHostAsync(ct);
        var effectiveLimit = limit > 0 ? limit : glab.PageSize;
        var project = GlabProjectPath.ApiId(owner, repo);
        var result = await glab.RunAsync(
            ["api", $"projects/{project}/repository/branches?per_page={effectiveLimit.ToString(CultureInfo.InvariantCulture)}"],
            GlabEnvironment.ForHost(host),
            ct);
        if (!result.IsSuccess)
        {
            throw GlabExceptions.FromExit($"api projects/{project}/repository/branches", result);
        }

        var branches = GlabBranchListParser.Parse(result.StandardOutput);
        return GhBranchFilter.Apply(branches, query, effectiveLimit);
    }
}
