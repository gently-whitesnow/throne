using System.Globalization;
using Microsoft.Extensions.Options;
using Throne.Application.Git;
using Throne.Infrastructure.Git.GitHubCli;

namespace Throne.Infrastructure.Git.GitLabCli;

internal sealed class GlabBranchLister(GlabCliInvoker glab, IOptions<GitLabSettings> settings)
{
    public async Task<IReadOnlyList<GitBranchRef>> ListAsync(
        string owner,
        string repo,
        string? query,
        int limit,
        CancellationToken ct)
    {
        var host = ReadHost();
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

    private string ReadHost()
    {
        var host = settings.Value.Host?.Trim();
        return string.IsNullOrWhiteSpace(host)
            ? throw new GitProviderException(GitProviderErrorKind.CliFailure, "Throne:GitLab:Host is not configured.")
            : host;
    }
}
