// MCP wire-format requires snake_case parameter names; tools are an API boundary.
#pragma warning disable CA1707
using System.ComponentModel;
using ModelContextProtocol.Server;
using Throne.Application.Intents;
using Throne.Domain.Intents;

namespace Throne.Api.Mcp.Tools;

[McpServerToolType]
public sealed class IntentTools(
    CreateIntentHandler create,
    GetIntentHandler get,
    ReadIntentTextHandler read)
{
    [McpServerTool(Name = "create_intent", UseStructuredContent = true)]
    [Description("Create a new Intent and seed v1 of its text. Returns the canonical Intent (id, current_version, text, tags, timestamps).")]
    public Task<Intent> CreateIntent(
        [Description("Initial text of the Intent. Must not be empty.")] string text,
        [Description("Optional tags for filtering Intents.")] IReadOnlyList<string>? tags,
        CancellationToken cancellationToken) =>
        create.HandleAsync(new CreateIntentCommand(text, tags), cancellationToken);

    [McpServerTool(Name = "get_intent", ReadOnly = true, UseStructuredContent = true)]
    [Description("Read an Intent by id. Always returns the full text along with current_version and tags.")]
    public Task<Intent> GetIntent(
        [Description("Intent identifier.")] string intent_id,
        CancellationToken cancellationToken) =>
        get.HandleAsync(new GetIntentQuery(intent_id), cancellationToken);

    [McpServerTool(Name = "read_intent_text", ReadOnly = true, UseStructuredContent = true)]
    [Description("Read a slice of Intent.text. Server hard limit per response: 64,000 characters; use next_start_line to paginate.")]
    public Task<TextSlice> ReadIntentText(
        [Description("Intent identifier.")] string intent_id,
        [Description("1-indexed first line to read. Default: 1.")] int? start_line,
        [Description("Number of lines to read. Default: until end-of-document under max_chars.")] int? line_count,
        [Description("Client-side max characters; capped to 64,000.")] int? max_chars,
        CancellationToken cancellationToken) =>
        read.HandleAsync(new ReadIntentTextQuery(intent_id, start_line, line_count, max_chars), cancellationToken);
}
