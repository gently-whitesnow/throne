using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Concatenates two <see cref="GitRepositoryRef"/> lists and uniques them by
/// <see cref="GitRepositoryRef.FullName"/>. Used by <see cref="GhRepoSearcher"/>
/// to overlay the <c>involved</c> scope on top of the operator's own repos
/// without duplicating entries.
/// </summary>
internal static class GhRepoMerger
{
    public static List<GitRepositoryRef> Merge(
        IReadOnlyList<GitRepositoryRef> primary,
        IReadOnlyList<GitRepositoryRef> secondary)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var merged = new List<GitRepositoryRef>(primary.Count + secondary.Count);
        foreach (var repo in primary.Concat(secondary))
        {
            if (seen.Add(repo.FullName))
            {
                merged.Add(repo);
            }
        }

        return merged;
    }
}
