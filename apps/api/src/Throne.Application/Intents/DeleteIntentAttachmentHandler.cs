using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Intents;

public sealed record DeleteIntentAttachmentCommand(string IntentId, string AttachmentId);

public sealed class DeleteIntentAttachmentHandler(
    IIntentRepository intents,
    IIntentAttachmentRepository attachments)
{
    public async Task HandleAsync(DeleteIntentAttachmentCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var intentId = new IntentId(command.IntentId);
        if (await intents.GetByIdAsync(intentId, ct).ConfigureAwait(false) is null)
        {
            throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{command.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = command.IntentId });
        }

        if (!await attachments.DeleteAsync(intentId, command.AttachmentId, ct).ConfigureAwait(false))
        {
            throw new ApiException(
                ErrorCodes.IntentAttachmentNotFound,
                $"Attachment '{command.AttachmentId}' not found.",
                new Dictionary<string, object?>
                {
                    ["intent_id"] = command.IntentId,
                    ["attachment_id"] = command.AttachmentId,
                });
        }
    }
}
