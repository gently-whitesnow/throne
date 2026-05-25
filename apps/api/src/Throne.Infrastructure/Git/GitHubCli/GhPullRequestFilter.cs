using System.Globalization;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// In-process substring filter for the PR typeahead. <c>gh pr list</c> already
/// honours <c>--state open</c> and <c>--limit</c>, so this helper only narrows
/// the page client-side using <c>#{number}</c> / title / head-ref tokens.
/// </summary>
internal static class GhPullRequestFilter
{
    public static IReadOnlyList<GitPullRequestRef> Apply(
        IReadOnlyList<GitPullRequestRef> prs,
        string? query,
        int limit)
    {
        IEnumerable<GitPullRequestRef> filtered = prs;
        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = prs.Where(pr => MatchesQuery(pr, query));
        }

        return limit > 0
            ? filtered.Take(limit).ToArray()
            : filtered.ToArray();
    }

    private static bool MatchesQuery(GitPullRequestRef pr, string query) =>
        pr.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
        || pr.HeadRef.Contains(query, StringComparison.OrdinalIgnoreCase)
        || pr.Number.ToString(CultureInfo.InvariantCulture).Contains(query, StringComparison.Ordinal);
}
