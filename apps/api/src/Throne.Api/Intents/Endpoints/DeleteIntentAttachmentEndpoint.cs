using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.Intents;

namespace Throne.Api.Intents;

public sealed class DeleteIntentAttachmentEndpoint(DeleteIntentAttachmentHandler handler)
{
    public async Task<IActionResult> RunAsync(string id, string attachmentId, CancellationToken cancellationToken)
    {
        try
        {
            await handler.HandleAsync(new DeleteIntentAttachmentCommand(id, attachmentId), cancellationToken);
            return new NoContentResult();
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.IntentNotFound)
        {
            return new NotFoundObjectResult(ApiProblems.NotFound("Intent not found", ex.Detail));
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.IntentAttachmentNotFound)
        {
            return new NotFoundObjectResult(ApiProblems.NotFound("Attachment not found", ex.Detail));
        }
    }
}
