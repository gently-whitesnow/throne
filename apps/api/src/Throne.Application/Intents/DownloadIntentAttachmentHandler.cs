using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Intents;

public sealed record DownloadIntentAttachmentQuery(string IntentId, string AttachmentId);

public sealed class DownloadIntentAttachmentHandler(
    IIntentRepository intents,
    IIntentAttachmentRepository attachments)
{
    public async Task<IntentAttachmentContent> HandleAsync(DownloadIntentAttachmentQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var intentId = new IntentId(query.IntentId);
        if (await intents.GetByIdAsync(intentId, ct) is null)
        {
            throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{query.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = query.IntentId });
        }

        return await attachments.OpenContentAsync(intentId, query.AttachmentId, ct)
            ?? throw new ApiException(
                ErrorCodes.IntentAttachmentNotFound,
                $"Attachment '{query.AttachmentId}' not found.",
                new Dictionary<string, object?>
                {
                    ["intent_id"] = query.IntentId,
                    ["attachment_id"] = query.AttachmentId,
                });
    }
}
