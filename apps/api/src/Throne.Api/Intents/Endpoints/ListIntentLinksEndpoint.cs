using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Throne.Application.Intents.Linking;
using Throne.Intents.Contracts.Generated;
using ContractIntentLinkDirection = Throne.Intents.Contracts.Generated.IntentLinkDirection;
using ContractIntentLinkType = Throne.Intents.Contracts.Generated.IntentLinkType;
using DomainIntentLinkDirection = Throne.Application.Ports.IntentLinkDirection;

namespace Throne.Api.Intents;

internal static class ListIntentLinksEndpoint
{
    public static async Task<ActionResult<IntentLinksPageDto>> RunAsync(
        string id,
        ContractIntentLinkDirection? direction,
        ContractIntentLinkType? type,
        int? limit,
        string? cursor,
        HttpContext http)
    {
        var handler = http.RequestServices.GetRequiredService<ListIntentLinksHandler>();
        var helpers = http.RequestServices.GetRequiredService<IntentsApiHelpers>();
        var page = await handler.HandleAsync(
            new ListIntentLinksQuery(
                id,
                MapDirection(direction),
                type is null ? null : IntentLinkDtoMapper.FromContractLinkType(type.Value),
                limit ?? ListIntentLinksHandler.DefaultLimit,
                cursor),
            http.RequestAborted);

        var tagMap = await helpers.BuildTagMapAsync(
            page.Items.SelectMany(v => v.Other.TagIds),
            http.RequestAborted);

        var dto = new IntentLinksPageDto
        {
            Items = new System.Collections.ObjectModel.Collection<IntentLinkViewDto>(
                [.. page.Items.Select(v => IntentLinkDtoMapper.ToLinkViewDto(v, tagMap))]),
            Next_cursor = page.NextCursor,
        };
        return new OkObjectResult(dto);
    }

    private static DomainIntentLinkDirection? MapDirection(ContractIntentLinkDirection? direction) => direction switch
    {
        ContractIntentLinkDirection.Outgoing => DomainIntentLinkDirection.Outgoing,
        ContractIntentLinkDirection.Incoming => DomainIntentLinkDirection.Incoming,
        _ => null,
    };
}
