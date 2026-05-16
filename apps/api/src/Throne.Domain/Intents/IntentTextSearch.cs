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

        contextLines = Math.Max(0, contextLines);
        limit = Math.Clamp(limit, 1, ServerMaxLimit);

        if (text.Length == 0)
        {
            return new IntentTextSearchResult([], TotalMatches: 0);
        }

        var lines = text.Split('\n');
        var lineStartOffsets = IntentTextSearchHelpers.ComputeLineStartOffsets(text);

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
                matches.Add(BuildMatch(idx, lines, lineStartOffsets, contextLines));
            }
            from = idx + query.Length;
        }

        return new IntentTextSearchResult(matches, totalMatches);
    }

    private static TextSearchMatch BuildMatch(int idx, string[] lines, int[] lineStartOffsets, int contextLines)
    {
        var (matchLine, matchColumn) = IntentTextSearchHelpers.LineAndColumn(lineStartOffsets, idx);
        var contextStartLine = Math.Max(1, matchLine - contextLines);
        var contextEndLine = Math.Min(lines.Length, matchLine + contextLines);
        var context = IntentTextSearchHelpers.JoinLines(lines, contextStartLine, contextEndLine);
        return new TextSearchMatch(matchLine, matchColumn, context, contextStartLine);
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
