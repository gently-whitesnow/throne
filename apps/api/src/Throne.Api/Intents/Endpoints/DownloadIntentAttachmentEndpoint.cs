using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.Intents;

namespace Throne.Api.Intents;

internal static class DownloadIntentAttachmentEndpoint
{
    public static async Task<IActionResult> RunAsync(string id, string attachmentId, HttpContext http)
    {
        var handler = http.RequestServices.GetRequiredService<DownloadIntentAttachmentHandler>();
        try
        {
            var attachment = await handler.HandleAsync(
                new DownloadIntentAttachmentQuery(id, attachmentId), http.RequestAborted);
            return new FileStreamResult(attachment.Content, attachment.Attachment.ContentType)
            {
                FileDownloadName = attachment.Attachment.FileName,
            };
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
