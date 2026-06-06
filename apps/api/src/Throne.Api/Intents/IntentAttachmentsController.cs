using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Intents.Contracts.Generated;
using FileParameter = Throne.Api.Generated.FileParameter;

namespace Throne.Api.Intents;

/// <summary>
/// HTTP controller for /api/v1/intents/{id}/attachments* — list / upload / download / delete.
/// One tag-scoped controller per intent sub-resource; bodies live in per-endpoint
/// instances (ListIntentAttachmentsEndpoint, UploadIntentAttachmentEndpoint,
/// DownloadIntentAttachmentEndpoint, DeleteIntentAttachmentEndpoint) injected via ctor.
/// </summary>
public sealed class IntentAttachmentsController(
    ListIntentAttachmentsEndpoint listIntentAttachments,
    UploadIntentAttachmentEndpoint uploadIntentAttachment,
    DownloadIntentAttachmentEndpoint downloadIntentAttachment,
    DeleteIntentAttachmentEndpoint deleteIntentAttachment) : IntentAttachmentsControllerBase
{
    public override Task<ActionResult<ICollection<IntentAttachmentDto>>> ListIntentAttachments(string id) =>
        listIntentAttachments.RunAsync(id, HttpContext.RequestAborted);

    [RequestFormLimits(MultipartBodyLengthLimit = 12 * 1024 * 1024)]
    public override Task<ActionResult<IntentAttachmentDto>> UploadIntentAttachment(string id, FileParameter file = default!) =>
        uploadIntentAttachment.RunAsync(id, HttpContext.Request, HttpContext.RequestAborted);

    public override Task<IActionResult> DownloadIntentAttachment(string id, string attachment_id) =>
        downloadIntentAttachment.RunAsync(id, attachment_id, HttpContext.RequestAborted);

    public override Task<IActionResult> DeleteIntentAttachment(string id, string attachment_id) =>
        deleteIntentAttachment.RunAsync(id, attachment_id, HttpContext.RequestAborted);
}
