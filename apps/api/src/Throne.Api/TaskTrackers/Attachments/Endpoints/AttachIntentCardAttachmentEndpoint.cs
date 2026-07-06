using Microsoft.AspNetCore.Mvc;
using Throne.Application.TaskTrackers.Attachments;
using Throne.CardAttachments.Contracts.Generated;

namespace Throne.Api.TaskTrackers.Attachments.Endpoints;

/// <summary>
/// Attaches a card as read-only context. Requires a live pull: a vanished/forbidden card is 404, an
/// unreachable tracker 502, a missing connection 409, an unsupported/invalid coordinate 422. Idempotent
/// by coordinate — a re-attach refreshes the snapshot and returns 200.
/// </summary>
public sealed class AttachIntentCardAttachmentEndpoint(CardAttachmentService service)
{
    public async Task<ActionResult<CardAttachmentDto>> RunAsync(
        string intentId, AttachCardRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var command = new AttachCardCommand(intentId, body.Tracker, body.Board_id, body.Card_id);
        var attachment = await service.AttachAsync(command, ct);
        return new OkObjectResult(CardAttachmentDtoMapper.ToDto(attachment));
    }
}
