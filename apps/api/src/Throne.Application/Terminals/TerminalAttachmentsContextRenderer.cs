using System.Globalization;
using System.Text;
using Throne.Application.Intents;
using Throne.Application.Intents.Attachments;

namespace Throne.Application.Terminals;

/// <summary>
/// Renders the intent's attachment metadata as a plain-text block appended to the embedded
/// terminal's user_prompt. Mirrors what <c>get_intent</c> exposes in standalone — the agent
/// then calls <c>read_intent_attachment_image</c> / <c>read_intent_attachment_text</c> via MCP
/// the same way as outside the embedded contour.
/// </summary>
public static class TerminalAttachmentsContextRenderer
{
    public const string BlockHeader = "[intent attachments]";

    public static string? Render(IReadOnlyList<IntentAttachment> attachments)
    {
        ArgumentNullException.ThrowIfNull(attachments);
        if (attachments.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.Append(BlockHeader).Append('\n');

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
                .Append("Use `").Append(AttachmentKindResolver.ToolNameImage)
                .Append("` (MCP) with the attachment id above to load image bytes as a vision block.")
                .Append('\n');
        }
        if (hasText)
        {
            builder.Append('\n')
                .Append("Use `").Append(AttachmentKindResolver.ToolNameText)
                .Append("` (MCP) with the attachment id above to read text content.")
                .Append('\n');
        }

        return builder.ToString().TrimEnd('\n');
    }

    private static string EscapeFileName(string fileName) =>
        fileName.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
}
