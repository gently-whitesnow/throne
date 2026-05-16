using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Tags;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

public sealed class GetIntentEndpoint(
    GetIntentHandler handler,
    IIntentLinkRepository linkRepository,
    IntentsApiHelpers helpers)
{
    public async Task<ActionResult<IntentDetailDto>> RunAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            var intent = await handler.HandleAsync(new GetIntentQuery(id), cancellationToken);
            var links = await linkRepository.ListByIntentAsync(intent.Id, cancellationToken);
            var tagMap = await helpers.BuildTagMapAsync(CollectTagIds(intent, links), cancellationToken);
            var linkDtos = links.Select(v => IntentLinkDtoMapper.ToLinkViewDto(v, tagMap)).ToList();
            var pinnedIn = await helpers.GetPinnedInAsync(intent.Id.Value, cancellationToken);
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
