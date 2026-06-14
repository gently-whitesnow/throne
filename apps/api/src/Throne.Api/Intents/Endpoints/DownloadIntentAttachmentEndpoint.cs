using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.Intents;

namespace Throne.Api.Intents;

public sealed class DownloadIntentAttachmentEndpoint(DownloadIntentAttachmentHandler handler)
{
    public async Task<IActionResult> RunAsync(
        string id,
        string attachmentId,
        CancellationToken cancellationToken
    )
    {
        var attachment = await handler.HandleAsync(
            new DownloadIntentAttachmentQuery(id, attachmentId),
            cancellationToken
        );
        return new FileStreamResult(attachment.Content, attachment.Attachment.ContentType)
        {
            FileDownloadName = attachment.Attachment.FileName,
        };
    }
}
