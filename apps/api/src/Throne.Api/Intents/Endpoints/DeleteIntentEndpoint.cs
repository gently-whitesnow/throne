using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.Intents;

namespace Throne.Api.Intents;

public sealed class DeleteIntentEndpoint(DeleteIntentHandler handler)
{
    public async Task<IActionResult> RunAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            await handler.HandleAsync(new DeleteIntentCommand(id), cancellationToken);
            return new NoContentResult();
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.IntentNotFound)
        {
            return new NotFoundObjectResult(ApiProblems.NotFound("Intent not found", ex.Detail));
        }
    }
}
