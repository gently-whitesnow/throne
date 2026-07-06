using Microsoft.AspNetCore.Mvc;
using Throne.Application.TaskTrackers.Attachments;
using Throne.CardAttachments.Contracts.Generated;

namespace Throne.Api.TaskTrackers.Attachments.Endpoints;

/// <summary>
/// Manual «Обновить» re-pull of an attachment's snapshot. Online-only and degrade-without-error: a
/// successful pull refreshes the snapshot and marks it <c>available</c>; an unreachable/unconnected
/// tracker keeps the snapshot and marks it <c>unavailable</c>; a vanished card marks it <c>gone</c>.
/// 404 only when the attachment is unknown for this intent.
/// </summary>
public sealed class RefreshIntentCardAttachmentEndpoint(CardAttachmentService service)
{
    public async Task<ActionResult<CardAttachmentDto>> RunAsync(
        string intentId, string attachmentId, CancellationToken ct)
    {
        var attachment = await service.RefreshAsync(
            new RefreshCardAttachmentCommand(intentId, attachmentId), ct);
        return new OkObjectResult(CardAttachmentDtoMapper.ToDto(attachment));
    }
}
