using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Domain.Intents.Training;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class SetIntentStatusEndpoint
{
    public static async Task<ActionResult<IntentDetailDto>> RunAsync(string id, SetIntentStatusRequest body, HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(body);
        var handler = http.RequestServices.GetRequiredService<SetIntentStatusHandler>();
        var helpers = http.RequestServices.GetRequiredService<IntentsApiHelpers>();
        try
        {
            var intent = await handler.HandleAsync(
                new SetIntentStatusCommand(
                    id,
                    IntentStatusDtoMapper.FromContractStatus(body.Status),
                    body.Reason,
                    IntentTrainingAuthor.User,
                    "http:set_intent_status"),
                http.RequestAborted);
            return new OkObjectResult(await IntentDetailDtoBuilder.BuildAsync(intent, helpers, http.RequestAborted));
        }
        catch (ApiException ex)
        {
            return IntentsErrorMapper.MapSetStatus(ex);
        }
    }
}
