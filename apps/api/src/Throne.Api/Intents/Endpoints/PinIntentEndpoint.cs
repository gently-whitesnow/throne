using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

public sealed class PinIntentEndpoint(PinIntentHandler handler, IntentsApiHelpers helpers)
{
    public async Task<ActionResult<IntentDetailDto>> RunAsync(
        string id,
        PinIntentRequest body,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var intent = await handler.HandleAsync(
                new PinIntentCommand(id, body.Context_tag_id, body.Before_id, body.After_id),
                cancellationToken);
            return new OkObjectResult(await IntentDetailDtoBuilder.BuildAsync(intent, helpers, cancellationToken));
        }
        catch (ApiException ex)
        {
            return IntentsErrorMapper.MapPin(ex);
        }
    }
}
