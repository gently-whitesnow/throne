using Throne.Domain.Tags;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class IntentTagDtoMapper
{
    public static List<TagRefDto> ToTagRefs(IReadOnlyList<TagId> tagIds, IReadOnlyDictionary<string, Tag> tagsById)
    {
        var refs = new List<TagRefDto>(tagIds.Count);
        foreach (var id in tagIds)
        {
            if (!tagsById.TryGetValue(id.Value, out var tag))
            {
                continue;
            }

            refs.Add(new TagRefDto { Id = tag.Id.Value, Name = tag.Name });
        }
        return refs;
    }
}
