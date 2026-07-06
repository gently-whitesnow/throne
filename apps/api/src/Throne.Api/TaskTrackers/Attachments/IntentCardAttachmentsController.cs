using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Api.TaskTrackers.Attachments.Endpoints;
using Throne.CardAttachments.Contracts.Generated;

namespace Throne.Api.TaskTrackers.Attachments;

/// <summary>
/// Card-attachment axis (ADR-0052): attach/detach/refresh/list of task-tracker cards as read-only intent
/// context. One class per OpenAPI tag; each override delegates to a per-operation endpoint.
/// </summary>
public sealed class IntentCardAttachmentsController(
    ListIntentCardAttachmentsEndpoint listEndpoint,
    AttachIntentCardAttachmentEndpoint attachEndpoint,
    DetachIntentCardAttachmentEndpoint detachEndpoint,
    RefreshIntentCardAttachmentEndpoint refreshEndpoint) : IntentCardAttachmentsControllerBase
{
    public override Task<ActionResult<ICollection<CardAttachmentDto>>> ListIntentCardAttachments(string intent_id) =>
        listEndpoint.RunAsync(intent_id, HttpContext.RequestAborted);

    public override Task<ActionResult<CardAttachmentDto>> AttachIntentCardAttachment(
        string intent_id,
        AttachCardRequest body) =>
        attachEndpoint.RunAsync(intent_id, body, HttpContext.RequestAborted);

    public override Task<IActionResult> DetachIntentCardAttachment(string intent_id, string attachment_id) =>
        detachEndpoint.RunAsync(intent_id, attachment_id, HttpContext.RequestAborted);

    public override Task<ActionResult<CardAttachmentDto>> RefreshIntentCardAttachment(
        string intent_id,
        string attachment_id) =>
        refreshEndpoint.RunAsync(intent_id, attachment_id, HttpContext.RequestAborted);
}
