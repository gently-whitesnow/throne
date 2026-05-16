using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class PinIntentEndpoint
{
    public static async Task<ActionResult<IntentDetailDto>> RunAsync(string id, PinIntentRequest body, HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(body);
        var handler = http.RequestServices.GetRequiredService<PinIntentHandler>();
        var helpers = http.RequestServices.GetRequiredService<IntentsApiHelpers>();
        try
        {
            var intent = await handler.HandleAsync(
                new PinIntentCommand(id, body.Context_tag_id, body.Before_id, body.After_id),
                http.RequestAborted);
            return new OkObjectResult(await IntentDetailDtoBuilder.BuildAsync(intent, helpers, http.RequestAborted));
        }
        catch (ApiException ex)
        {
            return IntentsErrorMapper.MapPin(ex);
        }
    }
}
