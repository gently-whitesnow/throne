using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Executes the two underlying <c>gh</c> calls that back the <c>mine</c> and
/// <c>involved</c> scopes (<c>gh repo list --json</c> and
/// <c>gh api /user/repos --paginate</c>).
/// </summary>
internal sealed class GhRepoListExecutor(GhCliInvoker gh)
{
    public async Task<IReadOnlyList<GitRepositoryRef>> MineAsync(int limit, CancellationToken ct)
    {
        var result = await gh.RunAsync(GhRepoListArgs.RepoList(limit), ct);
        if (!result.IsSuccess)
        {
            throw GhExceptions.FromExit("repo list", result);
        }

        return GhRepoListParser.Parse(result.StandardOutput);
    }

    public async Task<IReadOnlyList<GitRepositoryRef>> InvolvedAsync(int limit, CancellationToken ct)
    {
        var pageSize = Math.Min(limit, gh.PageSize);
        var result = await gh.RunAsync(GhRepoListArgs.UserReposPaginated(pageSize), ct);
        if (!result.IsSuccess)
        {
            throw GhExceptions.FromExit("api /user/repos", result);
        }

        return GhUserReposParser.Parse(NormalisePaginated(result.StandardOutput));
    }

    /// <summary>
    /// <c>gh api --paginate</c> emits concatenated JSON arrays (<c>][</c> at the
    /// boundary). Flatten them so the parser can consume the stream as one array.
    /// </summary>
    private static string NormalisePaginated(string raw) =>
        string.IsNullOrWhiteSpace(raw) ? "[]" : raw.Replace("][", ",", StringComparison.Ordinal);
}
