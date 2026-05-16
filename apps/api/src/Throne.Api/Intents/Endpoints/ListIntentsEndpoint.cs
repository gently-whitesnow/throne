using Microsoft.AspNetCore.Mvc;
using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Domain.Tags;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

public sealed class ListIntentsEndpoint(ListIntentsHandler handler, IntentsApiHelpers helpers)
{
    public const int TextShortMaxLength = 140;

    public async Task<ActionResult<ICollection<IntentListItemDto>>> RunAsync(
        IEnumerable<IntentStatus>? status,
        CancellationToken cancellationToken)
    {
        var statuses = status?
            .Select(IntentStatusDtoMapper.FromContractStatus)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var intents = await handler.HandleAsync(
            new ListIntentsQuery(statuses is { Length: > 0 } ? statuses : null), cancellationToken);

        var tagMap = await helpers.BuildTagMapAsync(intents.SelectMany(i => i.TagIds), cancellationToken);
        var pinnedMap = await helpers.GetPinnedInAsync(intents.Select(i => i.Id.Value).ToList(), cancellationToken);
        return new OkObjectResult(MapList(intents, tagMap, pinnedMap));
    }

    private static List<IntentListItemDto> MapList(
        IReadOnlyList<Intent> intents,
        IReadOnlyDictionary<string, Tag> tagMap,
        IReadOnlyDictionary<string, IReadOnlyList<IntentPin>> pinnedMap)
    {
        var dtos = new List<IntentListItemDto>(intents.Count);
        foreach (var intent in intents)
        {
            dtos.Add(IntentDtoMapper.ToListDto(
                intent,
                tagMap,
                TextShortMaxLength,
                pinnedMap.TryGetValue(intent.Id.Value, out var pinned) ? pinned : null));
        }
        return dtos;
    }
}
