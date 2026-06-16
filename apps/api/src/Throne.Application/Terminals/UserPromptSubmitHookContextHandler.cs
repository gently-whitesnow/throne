using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Terminals;

/// <summary>
/// Builds the <c>additionalContext</c> string the UserPromptSubmit hook returns to the embedded
/// Claude session. Mimics the standalone path where the agent learns about attachments by reading
/// <c>get_intent</c>; here Throne side-loads the same inventory into every user prompt so the
/// embedded agent can decide to call <c>read_intent_attachment_image</c> in the very same turn.
/// </summary>
public sealed class UserPromptSubmitHookContextHandler(IIntentAttachmentRepository attachments)
{
    public async Task<string?> BuildAsync(string intentId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);

        var items = await attachments.ListByIntentAsync(new IntentId(intentId), ct);
        return TerminalAttachmentsContextRenderer.Render(items);
    }
}
