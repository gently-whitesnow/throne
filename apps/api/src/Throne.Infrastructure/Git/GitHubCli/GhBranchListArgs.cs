using System.Globalization;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Argument builders for the <c>gh api</c> calls that back <see cref="GhBranchLister"/>.
/// The lister needs two upstream calls: one for the repo metadata (to know the
/// default branch) and one for the branches list itself.
/// </summary>
internal static class GhBranchListArgs
{
    public static string[] RepoView(string owner, string repo) =>
        ["api", $"/repos/{owner}/{repo}"];

    public static string[] Branches(string owner, string repo, int limit) =>
    [
        "api",
        $"/repos/{owner}/{repo}/branches?per_page={limit.ToString(CultureInfo.InvariantCulture)}",
    ];
}
