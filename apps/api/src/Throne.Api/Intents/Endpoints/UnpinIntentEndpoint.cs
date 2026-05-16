using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class UnpinIntentEndpoint
{
    public static async Task<ActionResult<IntentDetailDto>> RunAsync(string id, UnpinIntentRequest body, HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(body);
        var handler = http.RequestServices.GetRequiredService<UnpinIntentHandler>();
        var helpers = http.RequestServices.GetRequiredService<IntentsApiHelpers>();
        try
        {
            var intent = await handler.HandleAsync(
                new UnpinIntentCommand(id, body.Context_tag_id),
                http.RequestAborted);
            return new OkObjectResult(await IntentDetailDtoBuilder.BuildAsync(intent, helpers, http.RequestAborted));
        }
        catch (ApiException ex)
        {
            return IntentsErrorMapper.MapPin(ex);
        }
    }
}
