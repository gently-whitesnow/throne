using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Tags;

namespace Throne.Api.Mcp.Tools;

/// <summary>
/// Resolves canonical Tag rows into wire-shaped <see cref="McpTagRef"/> values.
/// Encapsulates the repository so MCP tool classes only depend on this
/// collaborator rather than ITagRepository — keeps tag DI off the tool ctors.
/// </summary>
public sealed class IntentToolTagRefs(ITagRepository tagRepository)
{
    public async Task<List<McpTagRef>> BuildAsync(IEnumerable<TagId> tagIds, CancellationToken ct)
    {
        var ids = tagIds as IList<TagId> ?? tagIds.ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var all = await tagRepository.ListAllAsync(ct);
        var refs = new List<McpTagRef>(ids.Count);
        foreach (var id in ids)
        {
            var tag = all.FirstOrDefault(t => t.Id.Value == id.Value);
            if (tag is null)
            {
                continue;
            }
            refs.Add(new McpTagRef(tag.Id.Value, tag.Name));
        }
        return refs;
    }

    public async Task<Dictionary<string, McpTagRef>> BuildIntentReadMapAsync(
        Intent intent,
        IReadOnlyList<IntentLinkView> links,
        CancellationToken ct)
    {
        var tagIds = new List<TagId>(intent.TagIds);
        foreach (var v in links)
        {
            tagIds.AddRange(v.Other.TagIds);
        }
        var refs = await BuildAsync(tagIds, ct);
        return refs
            .GroupBy(t => t.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }
}
