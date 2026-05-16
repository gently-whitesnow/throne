using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

public sealed class ListIntentAttachmentsEndpoint(ListIntentAttachmentsHandler handler)
{
    public async Task<ActionResult<ICollection<IntentAttachmentDto>>> RunAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            var attachments = await handler.HandleAsync(new ListIntentAttachmentsQuery(id), cancellationToken);
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
