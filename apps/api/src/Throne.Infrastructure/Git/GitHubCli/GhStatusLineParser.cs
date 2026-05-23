namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Parses the first line of a <c>gh api -i</c> response (e.g.
/// <c>HTTP/2.0 304 Not Modified</c>) into the numeric status code. Split out
/// from <see cref="GhHttpStatus"/> so each helper class stays inside the
/// CA1502 cyclomatic budget.
/// </summary>
internal static class GhStatusLineParser
{
    public static int ParseStatusCode(string raw)
    {
        var line = FirstLine(raw);
        if (line.Length == 0)
        {
            return 0;
        }

        var token = SecondToken(line);
        return int.TryParse(token, out var code) ? code : 0;
    }

    private static string FirstLine(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        var newline = raw.IndexOf('\n', StringComparison.Ordinal);
        var line = newline >= 0 ? raw[..newline] : raw;
        return line.TrimEnd('\r');
    }

    private static string SecondToken(string line)
    {
        var firstSpace = line.IndexOf(' ', StringComparison.Ordinal);
        if (firstSpace <= 0 || firstSpace == line.Length - 1)
        {
            return string.Empty;
        }

        var rest = line[(firstSpace + 1)..];
        var secondSpace = rest.IndexOf(' ', StringComparison.Ordinal);
        return secondSpace > 0 ? rest[..secondSpace] : rest;
    }
}
