using System.Globalization;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Builds argument vectors for the <c>gh repo list</c> / <c>gh api</c> calls
/// used by <see cref="GhRepoSearcher"/>. Extracted so the searcher stays inside
/// the CA1502 cyclomatic budget — every string-array literal counts towards the
/// per-type complexity score.
/// </summary>
internal static class GhRepoListArgs
{
    private const string JsonFields = "name,owner,defaultBranchRef,description,isPrivate,url,nameWithOwner";

    public static string[] RepoList(int limit) =>
    [
        "repo", "list",
        "--limit", limit.ToString(CultureInfo.InvariantCulture),
        "--json", JsonFields,
    ];

    public static string[] UserReposPaginated(int pageSize) =>
    [
        "api",
        $"/user/repos?affiliation=collaborator,organization_member&per_page={pageSize}",
        "--paginate",
    ];
}
