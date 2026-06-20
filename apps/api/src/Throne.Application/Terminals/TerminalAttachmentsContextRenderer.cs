using System.Text;
using Throne.Application.Intents;

namespace Throne.Application.Terminals;

/// <summary>
/// Renders the intent's attachments as a minimal block appended to the embedded terminal's
/// user_prompt. The bytes are already staged into the workspace on spawn
/// (<see cref="WorkspaceAttachmentDumper"/>), so each line carries only the original filename plus the
/// workspace-relative path the agent opens with a native <c>Read</c> — no <c>id</c>/<c>content_type</c>/
/// <c>size</c> metadata and no MCP attachment-read hint (token-economy: the embedded contour no longer
/// round-trips through those tools). The path is computed from metadata via
/// <see cref="WorkspaceAttachmentPaths"/>, the same source the dumper writes to, so prompt and disk agree.
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
        builder.Append("Files staged in this workspace — open with Read:").Append('\n');

        foreach (var att in attachments)
        {
            builder
                .Append("- \"").Append(EscapeFileName(att.FileName)).Append("\": ")
                .Append(WorkspaceAttachmentPaths.RelativePath(att.Id, att.FileName))
                .Append('\n');
        }

        return builder.ToString().TrimEnd('\n');
    }

    private static string EscapeFileName(string fileName) =>
        fileName.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
}
