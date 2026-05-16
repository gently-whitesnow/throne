using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.Intents;

namespace Throne.Api.Intents;

internal static class DeleteIntentEndpoint
{
    public static async Task<IActionResult> RunAsync(string id, HttpContext http)
    {
        var handler = http.RequestServices.GetRequiredService<DeleteIntentHandler>();
        try
        {
            await handler.HandleAsync(new DeleteIntentCommand(id), http.RequestAborted);
            return new NoContentResult();
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.IntentNotFound)
        {
            return new NotFoundObjectResult(ApiProblems.NotFound("Intent not found", ex.Detail));
        }
    }
}
