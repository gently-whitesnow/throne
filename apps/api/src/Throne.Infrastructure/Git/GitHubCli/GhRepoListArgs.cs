using System.Globalization;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Builds argument vectors for the <c>gh repo list</c> / <c>gh api</c> calls
/// used by <see cref="GhRepoSearcher"/>.
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
