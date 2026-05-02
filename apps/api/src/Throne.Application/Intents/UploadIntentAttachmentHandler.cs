using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Intents;

public sealed class UploadIntentAttachmentHandler(
    IIntentRepository intents,
    IIntentAttachmentRepository attachments,
    IUnitOfWork unitOfWork)
{
    public async Task<IntentAttachment> HandleAsync(UploadIntentAttachmentCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Content);

        if (command.ContentLength < 1)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "Uploaded file is empty.",
                new Dictionary<string, object?> { ["field"] = "file" });
        }

        if (command.ContentLength > IntentAttachmentLimits.MaxBytesPerFile)
        {
            throw new ApiException(
                ErrorCodes.IntentAttachmentTooLarge,
                $"File exceeds maximum size of {IntentAttachmentLimits.MaxBytesPerFile} bytes.",
                new Dictionary<string, object?>
                {
                    ["max_bytes"] = IntentAttachmentLimits.MaxBytesPerFile,
                    ["content_length"] = command.ContentLength,
                });
        }

        var intentId = new IntentId(command.IntentId);

        if (await intents.GetByIdAsync(intentId, ct).ConfigureAwait(false) is null)
        {
            throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{command.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = command.IntentId });
        }

        var count = await attachments.CountByIntentAsync(intentId, ct).ConfigureAwait(false);
        if (count >= IntentAttachmentLimits.MaxPerIntent)
        {
            throw new ApiException(
                ErrorCodes.IntentAttachmentLimitExceeded,
                $"An intent may have at most {IntentAttachmentLimits.MaxPerIntent} attachments.",
                new Dictionary<string, object?>
                {
                    ["max_attachments"] = IntentAttachmentLimits.MaxPerIntent,
                    ["intent_id"] = command.IntentId,
                });
        }

        var outcome = await unitOfWork.ExecuteOutsideTransactionAsync(
            inner => attachments.AddAsync(intentId, command.Content, command.FileName, command.ContentType, inner),
            ct).ConfigureAwait(false);
        return outcome.Attachment;
    }
}
