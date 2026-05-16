using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.Intents.Events;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class ListIntentEventsEndpoint
{
    public static async Task<ActionResult<ICollection<IntentEventDto>>> RunAsync(string id, HttpContext http)
    {
        var handler = http.RequestServices.GetRequiredService<ListIntentEventsHandler>();
        try
        {
            var events = await handler.HandleAsync(new ListIntentEventsQuery(id), http.RequestAborted);
            return new OkObjectResult(events.Select(IntentEventDtoMapper.ToEventDto).ToList());
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.IntentNotFound)
        {
            return new NotFoundObjectResult(ApiProblems.NotFound("Intent not found", ex.Detail));
        }
    }
}
