using System.Text;

namespace Throne.Infrastructure.EfCore.Search;

/// <summary>
/// Turns raw user input into a safe FTS5 MATCH expression. Each whitespace token becomes a
/// quoted prefix term (<c>"token"*</c>) and terms are ANDed — same intent as the previous
/// per-token LIKE filter, but ranked. Quoting neutralises FTS5 syntax characters in the
/// token (<c>* : " ( ) -</c> …) so arbitrary input cannot produce a syntax error; tokens
/// that carry no letter or digit are dropped. Returns <c>null</c> when nothing searchable
/// remains — callers treat that as «no results» (an empty MATCH string is a syntax error).
/// </summary>
internal static class Fts5MatchQuery
{
    private static readonly char[] Whitespace = [' ', '\t', '\r', '\n'];

    public static string? Build(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var token in input.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries))
        {
            var cleaned = token.Replace("\"", string.Empty, StringComparison.Ordinal);
            if (!cleaned.Any(char.IsLetterOrDigit))
            {
                continue;
            }
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }
            builder.Append('"').Append(cleaned).Append("\"*");
        }

        return builder.Length == 0 ? null : builder.ToString();
    }
}
