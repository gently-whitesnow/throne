using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Intents.Contracts.Generated;
using FileParameter = Throne.Api.Generated.FileParameter;

namespace Throne.Api.Intents;

/// <summary>
/// HTTP controller for /api/v1/intents/{id}/attachments* — list / upload / download / delete.
/// Split from <see cref="IntentsController"/> so each tag-scoped controller stays
/// under the CA1502 cyclomatic budget. Bodies live in per-endpoint static helpers
/// (ListIntentAttachmentsEndpoint, UploadIntentAttachmentEndpoint,
/// DownloadIntentAttachmentEndpoint, DeleteIntentAttachmentEndpoint).
/// </summary>
public sealed class IntentAttachmentsController : IntentAttachmentsControllerBase
{
    public override Task<ActionResult<ICollection<IntentAttachmentDto>>> ListIntentAttachments(string id) =>
        ListIntentAttachmentsEndpoint.RunAsync(id, HttpContext);

    [RequestFormLimits(MultipartBodyLengthLimit = 12 * 1024 * 1024)]
    public override Task<ActionResult<IntentAttachmentDto>> UploadIntentAttachment(string id, FileParameter file = default!) =>
        UploadIntentAttachmentEndpoint.RunAsync(id, HttpContext);

    public override Task<IActionResult> DownloadIntentAttachment(string id, string attachment_id) =>
        DownloadIntentAttachmentEndpoint.RunAsync(id, attachment_id, HttpContext);

    public override Task<IActionResult> DeleteIntentAttachment(string id, string attachment_id) =>
        DeleteIntentAttachmentEndpoint.RunAsync(id, attachment_id, HttpContext);
}
