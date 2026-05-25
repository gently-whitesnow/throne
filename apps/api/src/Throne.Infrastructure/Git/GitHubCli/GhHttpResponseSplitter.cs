namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Splits a <c>gh api -i</c> raw output (status line + headers + blank line + body)
/// into a header dictionary and body string.
/// </summary>
internal static class GhHttpResponseSplitter
{
    public readonly record struct Result(Dictionary<string, string> Headers, string Body);

    public static Result Split(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return new Result(EmptyHeaders(), string.Empty);
        }

        var (headerSegment, body) = SplitOnBlankLine(raw);
        return new Result(ParseHeaders(headerSegment), body);
    }

    private static (string Headers, string Body) SplitOnBlankLine(string raw)
    {
        var crlfIdx = raw.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        if (crlfIdx >= 0)
        {
            return (raw[..crlfIdx], raw[(crlfIdx + 4)..]);
        }

        var lfIdx = raw.IndexOf("\n\n", StringComparison.Ordinal);
        if (lfIdx >= 0)
        {
            return (raw[..lfIdx], raw[(lfIdx + 2)..]);
        }

        return (raw, string.Empty);
    }

    private static Dictionary<string, string> ParseHeaders(string headerSegment)
    {
        var headers = EmptyHeaders();
        foreach (var rawLine in headerSegment.Split('\n'))
        {
            TryAddHeader(headers, rawLine);
        }

        return headers;
    }

    private static void TryAddHeader(Dictionary<string, string> headers, string rawLine)
    {
        var line = rawLine.TrimEnd('\r');
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0)
        {
            return;
        }

        var name = line[..colon].Trim();
        var value = line[(colon + 1)..].Trim();
        headers[name] = value;
    }

    private static Dictionary<string, string> EmptyHeaders() => new(StringComparer.OrdinalIgnoreCase);
}
