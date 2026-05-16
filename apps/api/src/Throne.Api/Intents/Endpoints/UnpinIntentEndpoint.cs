using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

public sealed class UnpinIntentEndpoint(UnpinIntentHandler handler, IntentsApiHelpers helpers)
{
    public async Task<ActionResult<IntentDetailDto>> RunAsync(
        string id,
        UnpinIntentRequest body,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var intent = await handler.HandleAsync(
                new UnpinIntentCommand(id, body.Context_tag_id),
                cancellationToken);
            return new OkObjectResult(await IntentDetailDtoBuilder.BuildAsync(intent, helpers, cancellationToken));
        }
        catch (ApiException ex)
        {
            return IntentsErrorMapper.MapPin(ex);
        }
    }
}
