using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// In-process substring filter applied on top of <c>gh api branches</c> output.
/// Mirrors <see cref="RepoSearchFilter"/> — the upstream endpoint does not
/// support server-side filtering, so the typeahead query is matched here.
/// </summary>
internal static class GhBranchFilter
{
    public static IReadOnlyList<GitBranchRef> Apply(
        IReadOnlyList<GitBranchRef> branches,
        string? query,
        int limit)
    {
        IEnumerable<GitBranchRef> filtered = branches;
        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = branches.Where(b =>
                b.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        return limit > 0
            ? filtered.Take(limit).ToArray()
            : filtered.ToArray();
    }
}
