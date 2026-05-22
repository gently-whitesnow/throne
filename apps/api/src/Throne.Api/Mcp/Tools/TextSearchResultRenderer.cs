using System.Text;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using Throne.Application.Intents;

namespace Throne.Api.Mcp.Tools;

/// <summary>
/// Renders <see cref="TextSearchResult"/> for search_intent_text. Each match's context
/// excerpt lives in <see cref="TextContentBlock.Text"/>; wire <c>StructuredContent</c>
/// is <c>null</c> (ADR-0003 §8.1, 2026-05 amendment). Navigation metadata (line / column
/// / context_start_line) travels via the audit OOB envelope.
/// </summary>
internal static class TextSearchResultRenderer
{
    public static McpToolPayload Render(TextSearchResult search) => new(
        Wire: new CallToolResult
        {
            Content = [new TextContentBlock { Text = RenderText(search) }],
            StructuredContent = null,
            IsError = false,
        },
        AuditSummary: McpStructuredContent.ToAuditSummary(RenderStructured(search)));

    private static string RenderText(TextSearchResult r)
    {
        var sb = new StringBuilder(256);
        sb.Append("matches=").Append(r.Matches.Count);
        if (r.TotalMatchesEstimate is { } est)
        {
            sb.Append(" total_matches_estimate=").Append(est);
        }
        sb.Append('\n');

        var i = 1;
        foreach (var m in r.Matches)
        {
            sb.Append("\n#").Append(i++)
              .Append(" line=").Append(m.MatchLine)
              .Append(" column=").Append(m.MatchColumn)
              .Append(" context_start_line=").Append(m.ContextStartLine)
              .Append('\n')
              .Append(m.Context);
            if (m.Context.Length == 0 || !m.Context.EndsWith('\n'))
            {
                sb.Append('\n');
            }
        }

        return sb.ToString();
    }

    private static JsonObject RenderStructured(TextSearchResult r)
    {
        var refs = new JsonArray();
        foreach (var m in r.Matches)
        {
            refs.Add(new JsonObject
            {
                ["match_line"] = m.MatchLine,
                ["match_column"] = m.MatchColumn,
                ["context_start_line"] = m.ContextStartLine,
            });
        }

        return new JsonObject
        {
            ["match_count"] = r.Matches.Count,
            ["total_matches_estimate"] = r.TotalMatchesEstimate,
            ["matches"] = refs,
        };
    }
}
