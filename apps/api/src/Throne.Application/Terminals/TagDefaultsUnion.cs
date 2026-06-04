using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;
using Throne.Domain.Tags;

namespace Throne.Application.Terminals;

/// <summary>
/// Reads every <see cref="Tag"/> attached to an intent and returns the union of their
/// <see cref="Tag.DefaultRepositories"/>, deduped on the <c>(provider, owner, repo)</c>
/// coordinate. Lives in its own type so the orchestrating <see cref="RunPreflightAutoBind"/>
/// keeps the project-wide CA1502 type-level budget.
/// </summary>
public sealed class TagDefaultsUnion(ITagRepository tags)
{
    public async Task<IReadOnlyList<TagDefaultRepository>> ComputeAsync(Intent intent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (intent.TagIds.Count == 0)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var union = new List<TagDefaultRepository>();
        foreach (var tagId in intent.TagIds)
        {
            var tag = await tags.GetByIdAsync(tagId, ct);
            if (tag is null)
            {
                continue;
            }
            AppendUnique(tag, seen, union);
        }
        return union;
    }

    private static void AppendUnique(
        Tag tag,
        HashSet<string> seen,
        List<TagDefaultRepository> union)
    {
        foreach (var repo in tag.DefaultRepositories)
        {
            if (seen.Add(CoordinateKey(repo.Coordinate)))
            {
                union.Add(repo);
            }
        }
    }

    public static string CoordinateKey(RepoCoordinate coordinate) =>
        $"{coordinate.Provider}|{coordinate.Owner}|{coordinate.Repo}";
}
