using Throne.Application.Ports;
using Throne.Domain.Tags;

namespace Throne.Api.Intents;

internal static class IntentsApiTagMap
{
    public static async Task<IReadOnlyDictionary<string, Tag>> BuildAsync(
        ITagRepository tags,
        IEnumerable<TagId> tagIds,
        CancellationToken ct)
    {
        var ids = tagIds.Select(t => t.Value).Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<string, Tag>(StringComparer.Ordinal);
        }
        return FilterById(await tags.ListAllAsync(ct), ids);
    }

    private static Dictionary<string, Tag> FilterById(IReadOnlyList<Tag> all, List<string> ids)
    {
        var map = new Dictionary<string, Tag>(StringComparer.Ordinal);
        foreach (var t in all)
        {
            if (ids.Contains(t.Id.Value))
            {
                map[t.Id.Value] = t;
            }
        }
        return map;
    }
}
