using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Tags;

namespace Throne.Api.Mcp.Tools;

internal static class IntentReadTagMapBuilder
{
    public static async Task<Dictionary<string, McpTagRef>> BuildAsync(
        ITagRepository tagRepository,
        Intent intent,
        IReadOnlyList<IntentLinkView> links,
        CancellationToken ct)
    {
        var tagIds = new List<TagId>(intent.TagIds);
        foreach (var v in links)
        {
            tagIds.AddRange(v.Other.TagIds);
        }
        var refs = await IntentToolHelpers.BuildTagRefsAsync(tagRepository, tagIds, ct);
        return refs
            .GroupBy(t => t.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }
}
