using System.Text;

namespace Throne.Application.Terminals;

/// <summary>
/// Deterministic placement of intent attachments staged into the session workspace. Both the
/// byte dumper (writes the file) and the user_prompt renderer (prints the path the agent will
/// <c>Read</c>) compute the location through here, so the path in the prompt always matches the
/// file on disk without either side needing the bytes. The <c>{attachmentId}-</c> prefix removes
/// filename collisions and, together with sanitization, keeps the result inside
/// <see cref="RelativeDirectory"/> — a hostile filename cannot traverse out of it.
/// </summary>
public static class WorkspaceAttachmentPaths
{
    public const string RelativeDirectory = ".throne/attachments";

    public static string RelativePath(string attachmentId, string fileName) =>
        $"{RelativeDirectory}/{StagedFileName(attachmentId, fileName)}";

    public static string DirectoryPath(string workspacePath) =>
        Path.Combine(workspacePath, ".throne", "attachments");

    public static string AbsolutePath(string workspacePath, string attachmentId, string fileName) =>
        Path.Combine(DirectoryPath(workspacePath), StagedFileName(attachmentId, fileName));

    private static string StagedFileName(string attachmentId, string fileName) =>
        $"{attachmentId}-{Sanitize(fileName)}";

    private static string Sanitize(string fileName)
    {
        var builder = new StringBuilder(fileName.Length);
        foreach (var ch in fileName)
        {
            builder.Append(ch is '/' or '\\' or ':' || char.IsControl(ch) ? '_' : ch);
        }

        var sanitized = builder.ToString().Trim();
        return sanitized.Length == 0 ? "file" : sanitized;
    }
}
