using System.Globalization;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Argument vector for <c>gh pr list --state open --json</c>. Used by
/// <see cref="GhPullRequestLister"/> to back the PR combobox in the
/// bind-repository modal — only open PRs are returned in this iteration.
/// </summary>
internal static class GhPullRequestListArgs
{
    private const string JsonFields = "number,title,headRefName,state";

    public static string[] OpenList(string owner, string repo, int limit) =>
    [
        "pr", "list",
        "-R", $"{owner}/{repo}",
        "--state", "open",
        "--limit", limit.ToString(CultureInfo.InvariantCulture),
        "--json", JsonFields,
    ];
}
