namespace Throne.Domain.Intents;

public static class IntentTextSearch
{
    public const int ServerMaxLimit = 50;

    public static IntentTextSearchResult Search(
        string text,
        string query,
        int contextLines,
        int limit)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(query);
        if (query.Length == 0)
        {
            throw new ArgumentException("query must not be empty.", nameof(query));
        }

        if (contextLines < 0)
        {
            contextLines = 0;
        }

        if (limit < 1)
        {
            limit = 1;
        }

        if (limit > ServerMaxLimit)
        {
            limit = ServerMaxLimit;
        }

        if (text.Length == 0)
        {
            return new IntentTextSearchResult([], TotalMatches: 0);
        }

        var lines = text.Split('\n');
        var lineStartOffsets = ComputeLineStartOffsets(text);

        var matches = new List<TextSearchMatch>();
        var totalMatches = 0;
        var from = 0;
        while (true)
        {
            var idx = text.IndexOf(query, from, StringComparison.Ordinal);
            if (idx < 0)
            {
                break;
            }

            totalMatches++;
            if (matches.Count < limit)
            {
                var (matchLine, matchColumn) = LineAndColumn(lineStartOffsets, idx);
                var contextStartLine = Math.Max(1, matchLine - contextLines);
                var contextEndLine = Math.Min(lines.Length, matchLine + contextLines);
                var context = JoinLines(lines, contextStartLine, contextEndLine);
                matches.Add(new TextSearchMatch(matchLine, matchColumn, context, contextStartLine));
            }

            from = idx + query.Length;
        }

        return new IntentTextSearchResult(matches, totalMatches);
    }

    private static int[] ComputeLineStartOffsets(string text)
    {
        var offsets = new List<int> { 0 };
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                offsets.Add(i + 1);
            }
        }

        return [.. offsets];
    }

    private static (int Line, int Column) LineAndColumn(int[] lineStartOffsets, int index)
    {
        var lo = 0;
        var hi = lineStartOffsets.Length - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (lineStartOffsets[mid] <= index)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }

        var line = lo + 1;
        var column = index - lineStartOffsets[lo] + 1;
        return (line, column);
    }

    private static string JoinLines(string[] lines, int startLine1Indexed, int endLine1Indexed)
    {
        if (startLine1Indexed > endLine1Indexed)
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder();
        for (var i = startLine1Indexed - 1; i <= endLine1Indexed - 1; i++)
        {
            if (sb.Length > 0)
            {
                sb.Append('\n');
            }

            sb.Append(lines[i]);
        }

        return sb.ToString();
    }
}

public sealed record TextSearchMatch(
    int MatchLine,
    int MatchColumn,
    string Context,
    int ContextStartLine);

public sealed record IntentTextSearchResult(
    IReadOnlyList<TextSearchMatch> Matches,
    int TotalMatches);
