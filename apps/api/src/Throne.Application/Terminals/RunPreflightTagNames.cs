using Throne.Application.Ports;
using Throne.Domain.Tags;

namespace Throne.Application.Terminals;

/// <summary>
/// Resolves an intent's <see cref="TagId"/>s to tag names for the spawn prompt's workspace map.
/// Split into its own collaborator so <see cref="RunPreflightOrchestrator"/> stays a flat sequencer
/// (the per-stage types own the lookups). Ids that no longer resolve — a tag deleted between bind and
/// spawn — are dropped silently: the prompt is best-effort context, not a place to fail the run.
/// </summary>
public sealed class RunPreflightTagNames(ITagRepository tags)
{
    public async Task<IReadOnlyList<string>> ResolveAsync(
        IReadOnlyList<TagId> tagIds, CancellationToken ct)
    {
        if (tagIds is null || tagIds.Count == 0)
        {
            return [];
        }

        var names = new List<string>(tagIds.Count);
        foreach (var id in tagIds)
        {
            var tag = await tags.GetByIdAsync(id, ct);
            if (tag is not null)
            {
                names.Add(tag.Name);
            }
        }
        return names;
    }
}
