using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// In-process substring filter applied on top of <c>gh repo list</c> output —
/// ADR-0024 § 3 explicitly forbids the global <c>gh search repos</c> path, so
/// filtering by <c>query</c> happens client-side over the user's own / involved
/// repos only.
/// </summary>
internal static class RepoSearchFilter
{
    public static IReadOnlyList<GitRepositoryRef> Apply(
        IReadOnlyList<GitRepositoryRef> repos,
        string? query,
        int limit)
    {
        IEnumerable<GitRepositoryRef> filtered = repos;
        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = repos.Where(r =>
                r.Repo.Contains(query, StringComparison.OrdinalIgnoreCase)
                || r.FullName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (r.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return limit > 0
            ? filtered.Take(limit).ToArray()
            : filtered.ToArray();
    }
}
