using Microsoft.AspNetCore.Mvc;
using Throne.Application.TaskTrackers.Attachments;
using Throne.CardAttachments.Contracts.Generated;

namespace Throne.Api.TaskTrackers.Attachments.Endpoints;

/// <summary>
/// Lists the task-tracker cards attached to an intent, each with its last stored snapshot and
/// availability (never re-probed on read). 404 when the intent is unknown.
/// </summary>
public sealed class ListIntentCardAttachmentsEndpoint(
    CardAttachmentService service,
    CardAttachmentDtoMapper mapper)
{
    public async Task<ActionResult<ICollection<CardAttachmentDto>>> RunAsync(string intentId, CancellationToken ct)
    {
        var attachments = await service.ListAsync(intentId, ct);
        var dtos = await mapper.ToDtoListAsync(attachments, ct);
        return new OkObjectResult(dtos);
    }
}
