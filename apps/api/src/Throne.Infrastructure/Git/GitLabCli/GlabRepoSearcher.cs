using System.Globalization;
using Microsoft.Extensions.Options;
using Throne.Application.Git;
using Throne.Infrastructure.Git.GitHubCli;

namespace Throne.Infrastructure.Git.GitLabCli;

internal sealed class GlabRepoSearcher(GlabCliInvoker glab, IOptions<GitLabSettings> settings)
{
    public async Task<IReadOnlyList<GitRepositoryRef>> SearchAsync(
        RepositorySearchScope scope,
        string? query,
        int limit,
        CancellationToken ct)
    {
        var host = ReadHost();
        var effectiveLimit = limit > 0 ? limit : glab.PageSize;
        var mine = await ListAsync(host, owned: true, query, effectiveLimit, ct);
        var combined = await CombineForScopeAsync(scope, host, query, mine, effectiveLimit, ct);
        return RepoSearchFilter.Apply(combined, query, effectiveLimit);
    }

    private async Task<IReadOnlyList<GitRepositoryRef>> CombineForScopeAsync(
        RepositorySearchScope scope,
        string host,
        string? query,
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
            var member = await ListAsync(host, owned: false, query, limit, ct);
            return GhRepoMerger.Merge(mine, member);
        }

        throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown search scope.");
    }

    private async Task<IReadOnlyList<GitRepositoryRef>> ListAsync(
        string host,
        bool owned,
        string? query,
        int limit,
        CancellationToken ct)
    {
        var scope = owned ? "owned=true" : "membership=true";
        var path = BuildProjectsPath(scope, query, limit);
        var result = await glab.RunAsync(
            ["api", path],
            GlabEnvironment.ForHost(host),
            ct);
        if (!result.IsSuccess)
        {
            throw GlabExceptions.FromExit($"api projects?{scope}", result);
        }

        return GlabRepoListParser.Parse(result.StandardOutput, host);
    }

    // search_namespaces=true matches by full path (group/sub/repo), not just the project name,
    // so a query like "group/repo" reaches projects the user only sees through an ancestor group.
    private static string BuildProjectsPath(string scope, string? query, int limit)
    {
        var path = $"projects?{scope}&per_page={limit.ToString(CultureInfo.InvariantCulture)}";
        return string.IsNullOrWhiteSpace(query)
            ? path
            : $"{path}&search={Uri.EscapeDataString(query)}&search_namespaces=true";
    }

    private string ReadHost()
    {
        var host = settings.Value.Host?.Trim();
        return string.IsNullOrWhiteSpace(host)
            ? throw new GitProviderException(
                GitProviderErrorKind.CliFailure,
                "Throne:GitLab:Host is not configured.",
                detail: null)
            : host;
    }
}
