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
        var mine = await ListAsync(host, owned: true, effectiveLimit, ct);
        var combined = await CombineForScopeAsync(scope, host, mine, effectiveLimit, ct);
        return RepoSearchFilter.Apply(combined, query, effectiveLimit);
    }

    private async Task<IReadOnlyList<GitRepositoryRef>> CombineForScopeAsync(
        RepositorySearchScope scope,
        string host,
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
            var member = await ListAsync(host, owned: false, limit, ct);
            return GhRepoMerger.Merge(mine, member);
        }

        throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown search scope.");
    }

    private async Task<IReadOnlyList<GitRepositoryRef>> ListAsync(
        string host,
        bool owned,
        int limit,
        CancellationToken ct)
    {
        var scope = owned ? "owned=true" : "membership=true";
        var result = await glab.RunAsync(
            ["api", $"projects?{scope}&per_page={limit.ToString(CultureInfo.InvariantCulture)}"],
            GlabEnvironment.ForHost(host),
            ct);
        if (!result.IsSuccess)
        {
            throw GlabExceptions.FromExit($"api projects?{scope}", result);
        }

        return GlabRepoListParser.Parse(result.StandardOutput, host);
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
