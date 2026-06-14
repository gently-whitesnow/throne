using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.Intents;

namespace Throne.Api.Intents;

public sealed class DeleteIntentAttachmentEndpoint(DeleteIntentAttachmentHandler handler)
{
    public async Task<IActionResult> RunAsync(
        string id,
        string attachmentId,
        CancellationToken cancellationToken
    )
    {
        await handler.HandleAsync(
            new DeleteIntentAttachmentCommand(id, attachmentId),
            cancellationToken
        );
        return new NoContentResult();
    }
}
