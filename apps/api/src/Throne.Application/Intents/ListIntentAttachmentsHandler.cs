using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Intents;

public sealed record ListIntentAttachmentsQuery(string IntentId);

public sealed class ListIntentAttachmentsHandler(
    IIntentRepository intents,
    IIntentAttachmentRepository attachments)
{
    public async Task<IReadOnlyList<IntentAttachment>> HandleAsync(ListIntentAttachmentsQuery query, CancellationToken ct)
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

        return await attachments.ListByIntentAsync(intentId, ct);
    }
}
