using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Domain.Tags;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class ListIntentsEndpoint
{
    public const int TextShortMaxLength = 140;

    public static async Task<ActionResult<ICollection<IntentListItemDto>>> RunAsync(IEnumerable<IntentStatus>? status, HttpContext http)
    {
        var handler = http.RequestServices.GetRequiredService<ListIntentsHandler>();
        var helpers = http.RequestServices.GetRequiredService<IntentsApiHelpers>();

        var statuses = status?
            .Select(IntentStatusDtoMapper.FromContractStatus)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var intents = await handler.HandleAsync(
            new ListIntentsQuery(statuses is { Length: > 0 } ? statuses : null), http.RequestAborted);

        var tagMap = await helpers.BuildTagMapAsync(intents.SelectMany(i => i.TagIds), http.RequestAborted);
        var pinnedMap = await helpers.GetPinnedInAsync(intents.Select(i => i.Id.Value).ToList(), http.RequestAborted);
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
