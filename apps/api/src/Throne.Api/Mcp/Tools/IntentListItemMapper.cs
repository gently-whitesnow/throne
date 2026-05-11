using Throne.Domain.Intents;

namespace Throne.Api.Mcp.Tools;

internal static class IntentListItemMapper
{
    public static McpIntentListItem ToItem(Intent intent, IReadOnlyDictionary<string, McpTagRef> tagsById) => new(
        Id: intent.Id.Value,
        Status: intent.Status,
        CurrentVersion: intent.CurrentVersion,
        Tags: ResolveTags(intent.TagIds, tagsById),
        Preview: IntentToolHelpers.BuildPreview(intent.Text),
        SortKey: intent.SortKey,
        CreatedAt: intent.CreatedAt,
        UpdatedAt: intent.UpdatedAt);

    private static List<McpTagRef> ResolveTags(
        IReadOnlyList<Throne.Domain.Tags.TagId> tagIds,
        IReadOnlyDictionary<string, McpTagRef> tagsById)
    {
        var result = new List<McpTagRef>(tagIds.Count);
        foreach (var id in tagIds)
        {
            if (tagsById.TryGetValue(id.Value, out var t))
            {
                result.Add(t);
            }
        }
        return result;
    }
}
