using System.Globalization;
using System.Text;
using Throne.Application.Intents;
using Throne.Application.Intents.Attachments;

namespace Throne.Application.Terminals;

/// <summary>
/// Renders the intent's attachment metadata as a plain-text block appended to the embedded
/// terminal's user_prompt. Mirrors what <c>get_intent</c> exposes in standalone — the agent
/// then reads the bytes via the attachment-read MCP tools the same way as outside the embedded
/// contour. The hint deliberately names the tool by capability, not by its bare wire name: each
/// MCP client registers these tools under its own server prefix (<c>mcp__throne__…</c> vs
/// <c>throne_…</c>), so a bare name is not directly callable and weaker models copied it verbatim.
/// </summary>
public static class TerminalAttachmentsContextRenderer
{
    public const string BlockHeader = "[intent attachments]";

    public static string? Render(string intentId, IReadOnlyList<IntentAttachment> attachments)
    {
        ArgumentNullException.ThrowIfNull(intentId);
        ArgumentNullException.ThrowIfNull(attachments);
        if (attachments.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.Append(BlockHeader).Append('\n');
        builder.Append("intent_id=").Append(intentId).Append('\n');

        var hasImage = false;
        var hasText = false;
        foreach (var att in attachments)
        {
            var kind = AttachmentKindResolver.Resolve(att.ContentType);
            hasImage |= kind == AttachmentKind.Image;
            hasText |= kind == AttachmentKind.Text;
            builder
                .Append("- id=").Append(att.Id)
                .Append(" kind=").Append(AttachmentKindResolver.KindWireName(kind))
                .Append(" filename=\"").Append(EscapeFileName(att.FileName)).Append('"')
                .Append(" content_type=").Append(att.ContentType)
                .Append(" size_bytes=").Append(att.SizeBytes.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }

        if (hasImage)
        {
            builder.Append('\n')
                .Append("To view an image attachment, call your MCP client's attachment-read tool for images ")
                .Append("(the one that loads image bytes as a vision block), passing intent_id and the attachment id above.")
                .Append('\n');
        }
        if (hasText)
        {
            builder.Append('\n')
                .Append("To read a text attachment, call your MCP client's attachment-read tool for text/log files, ")
                .Append("passing intent_id and the attachment id above.")
                .Append('\n');
        }

        return builder.ToString().TrimEnd('\n');
    }

    private static string EscapeFileName(string fileName) =>
        fileName.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
}
