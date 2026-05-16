using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class ListIntentAttachmentsEndpoint
{
    public static async Task<ActionResult<ICollection<IntentAttachmentDto>>> RunAsync(string id, HttpContext http)
    {
        var handler = http.RequestServices.GetRequiredService<ListIntentAttachmentsHandler>();
        try
        {
            var attachments = await handler.HandleAsync(new ListIntentAttachmentsQuery(id), http.RequestAborted);
            var dtos = new List<IntentAttachmentDto>(attachments.Count);
            foreach (var attachment in attachments)
            {
                dtos.Add(IntentDtoMapper.ToAttachmentDto(attachment));
            }
            return new OkObjectResult(dtos);
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.IntentNotFound)
        {
            return new NotFoundObjectResult(ApiProblems.NotFound("Intent not found", ex.Detail));
        }
    }
}
