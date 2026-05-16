using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

public sealed class SetIntentTagsEndpoint(SetIntentTagsHandler handler, IntentsApiHelpers helpers)
{
    public async Task<ActionResult<IntentDetailDto>> RunAsync(
        string id,
        SetIntentTagsRequest body,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var intent = await handler.HandleAsync(
                new SetIntentTagsCommand(id, body.Expected_version, TagIds: null, body.Tag_names?.ToList()),
                cancellationToken);
            return new OkObjectResult(await IntentDetailDtoBuilder.BuildAsync(intent, helpers, cancellationToken));
        }
        catch (ApiException ex)
        {
            return IntentsErrorMapper.MapSetTags(ex);
        }
    }
}
