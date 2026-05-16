using Throne.Domain.Intents;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class IntentPinDtoMapper
{
    public static System.Collections.ObjectModel.Collection<PinnedContextDto> ToPinnedContexts(
        IReadOnlyList<IntentPin>? pins)
    {
        if (pins is null || pins.Count == 0)
        {
            return new System.Collections.ObjectModel.Collection<PinnedContextDto>();
        }
        var list = new List<PinnedContextDto>(pins.Count);
        foreach (var pin in pins)
        {
            list.Add(new PinnedContextDto
            {
                Context_tag_id = pin.ContextTagId.Value,
                Pin_sort_key = pin.PinSortKey,
            });
        }
        return new System.Collections.ObjectModel.Collection<PinnedContextDto>(list);
    }
}
