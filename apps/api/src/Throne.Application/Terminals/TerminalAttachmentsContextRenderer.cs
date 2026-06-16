using System.Text;
using Throne.Application.Intents;
using Throne.Application.Intents.Attachments;

namespace Throne.Application.Terminals;

/// <summary>
/// Renders the <c>[intent attachments]</c> block injected into Claude's UserPromptSubmit context so
/// the embedded agent learns about attachments without a prior <c>get_intent</c> tool call (ADR-0034
/// embedded ↔ standalone parity for vision blocks). Returns <c>null</c> when there is nothing to
/// advertise — the controller then leaves the response body empty and no system reminder is
/// appended to the user prompt.
/// </summary>
public static class TerminalAttachmentsContextRenderer
{
    public static string? Render(IReadOnlyList<IntentAttachment> attachments)
    {
        ArgumentNullException.ThrowIfNull(attachments);
        if (attachments.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder("[intent attachments]");
        foreach (var attachment in attachments)
        {
            var kind = AttachmentKindResolver.KindWireName(
                AttachmentKindResolver.Resolve(attachment.ContentType));
            builder.Append('\n')
                .Append("- id=").Append(attachment.Id)
                .Append(" kind=").Append(kind)
                .Append(" filename=").Append(attachment.FileName);
        }

        return builder.ToString();
    }
}
