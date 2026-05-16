using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class MoveIntentEndpoint
{
    public static async Task<ActionResult<IntentDetailDto>> RunAsync(string id, MoveIntentRequest body, HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(body);
        var handler = http.RequestServices.GetRequiredService<MoveIntentHandler>();
        var helpers = http.RequestServices.GetRequiredService<IntentsApiHelpers>();
        try
        {
            var intent = await handler.HandleAsync(new MoveIntentCommand(id, body.Before_id, body.After_id), http.RequestAborted);
            return new OkObjectResult(await IntentDetailDtoBuilder.BuildAsync(intent, helpers, http.RequestAborted));
        }
        catch (ApiException ex)
        {
            return IntentsErrorMapper.MapMove(ex);
        }
    }
}
