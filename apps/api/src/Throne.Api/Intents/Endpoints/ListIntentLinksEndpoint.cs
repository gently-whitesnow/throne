using Microsoft.AspNetCore.Mvc;
using Throne.Application.Intents.Linking;
using Throne.Intents.Contracts.Generated;
using ContractIntentLinkDirection = Throne.Intents.Contracts.Generated.IntentLinkDirection;
using DomainIntentLinkDirection = Throne.Application.Ports.IntentLinkDirection;

namespace Throne.Api.Intents;

public sealed class ListIntentLinksEndpoint(ListIntentLinksHandler handler, IntentsApiHelpers helpers)
{
    public async Task<ActionResult<IntentLinksPageDto>> RunAsync(
        string id,
        ContractIntentLinkDirection? direction,
        bool? blocking,
        int? limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var page = await handler.HandleAsync(
            new ListIntentLinksQuery(
                id,
                MapDirection(direction),
                blocking,
                limit ?? ListIntentLinksHandler.DefaultLimit,
                cursor),
            cancellationToken);

        var tagMap = await helpers.BuildTagMapAsync(
            page.Items.SelectMany(v => v.Other.TagIds),
            cancellationToken);

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
