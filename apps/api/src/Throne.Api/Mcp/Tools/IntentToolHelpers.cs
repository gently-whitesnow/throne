using Throne.Application.Ports;
using Throne.Domain.Tags;

namespace Throne.Api.Mcp.Tools;

internal static class IntentToolHelpers
{
    public static async Task<List<McpTagRef>> BuildTagRefsAsync(
        ITagRepository tagRepository,
        IEnumerable<TagId> tagIds,
        CancellationToken ct)
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

    public static string BuildPreview(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }
            return trimmed.Length <= 200 ? trimmed : trimmed[..200];
        }
        return string.Empty;
    }
}
