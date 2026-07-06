using Microsoft.AspNetCore.Mvc;
using Throne.Application.TaskTrackers.Attachments;

namespace Throne.Api.TaskTrackers.Attachments.Endpoints;

/// <summary>
/// Detaches a card from an intent. Idempotent — returns 204 whether or not the attachment existed (or
/// belonged to another intent).
/// </summary>
public sealed class DetachIntentCardAttachmentEndpoint(CardAttachmentService service)
{
    public async Task<IActionResult> RunAsync(string intentId, string attachmentId, CancellationToken ct)
    {
        await service.DetachAsync(new DetachCardCommand(intentId, attachmentId), ct);
        return new NoContentResult();
    }
}
