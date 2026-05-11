namespace Throne.Application.Intents;

internal static class IntentTextSlicer
{
    public const int ServerMaxChars = 64_000;

    public static TextSlice Slice(int currentVersion, string text, int? startLine, int? lineCount, int? maxChars)
    {
        var lines = text.Length == 0 ? [] : text.Split('\n');
        var totalLines = lines.Length;
        var firstLine = Math.Max(1, startLine ?? 1);

        if (totalLines == 0)
        {
            return Empty(currentVersion, startLine: 1, endLine: 0, totalLines: 0);
        }
        if (firstLine > totalLines)
        {
            return Empty(currentVersion, firstLine, firstLine - 1, totalLines);
        }

        var requested = Math.Max(0, lineCount ?? (totalLines - firstLine + 1));
        var charLimit = Math.Min(maxChars ?? ServerMaxChars, ServerMaxChars);
        var window = LineWindow.Take(lines, firstLine, requested, charLimit);

        int? nextStartLine = window.Truncated && window.EndLine < totalLines ? window.EndLine + 1 : null;
        return new TextSlice(
            CurrentVersion: currentVersion,
            StartLine: firstLine,
            EndLine: window.EndLine,
            TotalLines: totalLines,
            Content: window.Content,
            Truncated: window.Truncated,
            NextStartLine: nextStartLine);
    }

    private static TextSlice Empty(int currentVersion, int startLine, int endLine, int totalLines) =>
        new(currentVersion, startLine, endLine, totalLines, string.Empty, Truncated: false, NextStartLine: null);
}

internal static class LineWindow
{
    public readonly record struct Result(string Content, int EndLine, bool Truncated);

    public static Result Take(string[] lines, int firstLine, int requested, int charLimit)
    {
        var sb = new System.Text.StringBuilder();
        var endLine = firstLine - 1;
        for (var i = 0; i < requested && (firstLine - 1 + i) < lines.Length; i++)
        {
            var line = lines[firstLine - 1 + i];
            var addition = i == 0 ? line : "\n" + line;
            if (sb.Length + addition.Length > charLimit)
            {
                return new Result(sb.ToString(), endLine, Truncated: true);
            }
            sb.Append(addition);
            endLine = firstLine + i;
        }
        return new Result(sb.ToString(), endLine, Truncated: false);
    }
}
