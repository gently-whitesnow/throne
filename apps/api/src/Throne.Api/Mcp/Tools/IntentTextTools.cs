// MCP wire-format requires snake_case parameter names; tools are an API boundary.
#pragma warning disable CA1707
using System.ComponentModel;
using ModelContextProtocol.Server;
using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Domain.TextVersions;

namespace Throne.Api.Mcp.Tools;

[McpServerToolType]
public sealed class IntentTextTools(
    ReadIntentTextHandler read,
    ReplaceIntentTextHandler replace,
    InsertIntentTextAfterLineHandler insertAfterLine,
    SearchIntentTextHandler search)
{
    [McpServerTool(Name = "read_intent_text", ReadOnly = true, UseStructuredContent = true)]
    [Description("Read a line range from Intent.text. Use for large documents or before line-based insertions. Server caps each response at 64,000 characters; paginate with next_start_line.")]
    public Task<TextSlice> ReadIntentText(
        [Description("Intent id to read.")] string intent_id,
        [Description("1-based first line to read. Defaults to 1.")] int? start_line = null,
        [Description("Maximum number of lines to read. Omit to read until end-of-document or max_chars.")] int? line_count = null,
        [Description("Client-requested character budget. The server may return fewer characters and never more than 64,000.")] int? max_chars = null,
        CancellationToken cancellationToken = default) =>
        read.HandleAsync(new ReadIntentTextQuery(intent_id, start_line, line_count, max_chars), cancellationToken);

    [McpServerTool(Name = "replace_intent_text", UseStructuredContent = true)]
    [Description("Replace one unique byte-exact substring in Intent.text using optimistic concurrency. Prefer this for precise edits; never use it as a casual full-document rewrite.")]
    public Task<Intent> ReplaceIntentText(
        [Description("Intent id to mutate.")] string intent_id,
        [Description("current_version observed from the latest get_intent, read_intent_text, or write result.")] int expected_version,
        [Description("Exact substring to replace. Whitespace and newlines are significant, and the substring must occur exactly once.")] string old_text,
        [Description("Replacement text. Use an empty string only when intentionally deleting the matched fragment.")] string new_text,
        CancellationToken cancellationToken) =>
        replace.HandleAsync(new ReplaceIntentTextCommand(intent_id, expected_version, old_text, new_text, TextVersionAuthor.Agent), cancellationToken);

    [McpServerTool(Name = "insert_intent_text_after_line", UseStructuredContent = true)]
    [Description("Insert text after a line in Intent.text using optimistic concurrency. Use after_line=0 to prepend and after_line=total_lines to append.")]
    public Task<Intent> InsertIntentTextAfterLine(
        [Description("Intent id to mutate.")] string intent_id,
        [Description("current_version observed from the latest get_intent, read_intent_text, or write result.")] int expected_version,
        [Description("Line number to insert after, in the inclusive range 0..total_lines.")] int after_line,
        [Description("Text to insert. May span multiple lines; include any required leading/trailing newline yourself.")] string insert_text,
        CancellationToken cancellationToken) =>
        insertAfterLine.HandleAsync(new InsertIntentTextAfterLineCommand(intent_id, expected_version, after_line, insert_text), cancellationToken);

    [McpServerTool(Name = "search_intent_text", ReadOnly = true, UseStructuredContent = true)]
    [Description("Search Intent.text for a case-sensitive byte-exact substring. Use before replace_intent_text when you need unique context for a safe edit.")]
    public Task<TextSearchResult> SearchIntentText(
        [Description("Intent id to search.")] string intent_id,
        [Description("Case-sensitive byte-exact substring to find.")] string query,
        [Description("Number of surrounding lines to include for each match. Defaults to 3.")] int? context_lines = null,
        [Description("Maximum matches to return. Defaults to 10 and is capped at 50.")] int? limit = null,
        CancellationToken cancellationToken = default) =>
        search.HandleAsync(new SearchIntentTextQuery(intent_id, query, context_lines, limit), cancellationToken);
}
