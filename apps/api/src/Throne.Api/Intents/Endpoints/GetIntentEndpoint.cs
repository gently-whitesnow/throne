using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Tags;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class GetIntentEndpoint
{
    public static async Task<ActionResult<IntentDetailDto>> RunAsync(string id, HttpContext http)
    {
        var handler = http.RequestServices.GetRequiredService<GetIntentHandler>();
        var linkRepository = http.RequestServices.GetRequiredService<IIntentLinkRepository>();
        var helpers = http.RequestServices.GetRequiredService<IntentsApiHelpers>();
        try
        {
            var intent = await handler.HandleAsync(new GetIntentQuery(id), http.RequestAborted);
            var links = await linkRepository.ListByIntentAsync(intent.Id, http.RequestAborted);
            var tagMap = await helpers.BuildTagMapAsync(CollectTagIds(intent, links), http.RequestAborted);
            var linkDtos = links.Select(v => IntentLinkDtoMapper.ToLinkViewDto(v, tagMap)).ToList();
            var pinnedIn = await helpers.GetPinnedInAsync(intent.Id.Value, http.RequestAborted);
            return new OkObjectResult(IntentDtoMapper.ToDetailDto(intent, tagMap, linkDtos, pinnedIn));
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.IntentNotFound)
        {
            return new NotFoundObjectResult(ApiProblems.NotFound("Intent not found", ex.Detail));
        }
    }

    private static IEnumerable<TagId> CollectTagIds(Intent intent, IReadOnlyList<IntentLinkView> links)
    {
        foreach (var id in intent.TagIds)
        {
            yield return id;
        }
        foreach (var view in links)
        {
            foreach (var id in view.Other.TagIds)
            {
                yield return id;
            }
        }
    }
}
