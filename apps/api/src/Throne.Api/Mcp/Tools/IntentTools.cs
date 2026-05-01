// MCP wire-format requires snake_case parameter names; tools are an API boundary.
#pragma warning disable CA1707
using System.ComponentModel;
using ModelContextProtocol.Server;
using Throne.Application.Instructions;
using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Domain.TextVersions;

namespace Throne.Api.Mcp.Tools;

[McpServerToolType]
public sealed class IntentTools(
    CreateIntentHandler create,
    GetIntentHandler get,
    ReadIntentTextHandler read,
    ReplaceIntentTextHandler replace,
    InsertIntentTextAfterLineHandler insertAfterLine,
    SearchIntentTextHandler search,
    AddIntentQaHandler addQa,
    AddIntentReviewHandler addReview,
    GetInstructionBundleHandler getInstructionBundle)
{
    [McpServerTool(Name = "create_intent", UseStructuredContent = true)]
    [Description("Create a new Intent and seed v1 of its text. Returns the canonical Intent (id, current_version, text, tags, timestamps).")]
    public Task<Intent> CreateIntent(
        [Description("Initial text of the Intent. Must not be empty.")] string text,
        [Description("Optional tags for filtering Intents.")] IReadOnlyList<string>? tags,
        CancellationToken cancellationToken) =>
        create.HandleAsync(new CreateIntentCommand(text, tags, TextVersionAuthor.Agent), cancellationToken);

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
        [Description("1-indexed first line to read. Default: 1.")] int? start_line = null,
        [Description("Number of lines to read. Default: until end-of-document under max_chars.")] int? line_count = null,
        [Description("Client-side max characters; capped to 64,000.")] int? max_chars = null,
        CancellationToken cancellationToken = default) =>
        read.HandleAsync(new ReadIntentTextQuery(intent_id, start_line, line_count, max_chars), cancellationToken);

    [McpServerTool(Name = "replace_intent_text", UseStructuredContent = true)]
    [Description("Replace a unique substring of Intent.text with optimistic concurrency. Errors: intent.version_conflict, intent.text.match_not_found, intent.text.match_ambiguous.")]
    public Task<Intent> ReplaceIntentText(
        [Description("Intent identifier.")] string intent_id,
        [Description("Expected current_version of the Intent.")] int expected_version,
        [Description("Exact byte-for-byte substring to replace. Must occur exactly once in Intent.text.")] string old_text,
        [Description("Replacement text. May be empty (deletes the matched fragment).")] string new_text,
        CancellationToken cancellationToken) =>
        replace.HandleAsync(new ReplaceIntentTextCommand(intent_id, expected_version, old_text, new_text, TextVersionAuthor.Agent), cancellationToken);

    [McpServerTool(Name = "insert_intent_text_after_line", UseStructuredContent = true)]
    [Description("Insert text after a given 1-indexed line in Intent.text. after_line=0 prepends; after_line=total_lines appends. Errors: intent.version_conflict, intent.text.line_out_of_range.")]
    public Task<Intent> InsertIntentTextAfterLine(
        [Description("Intent identifier.")] string intent_id,
        [Description("Expected current_version of the Intent.")] int expected_version,
        [Description("Line index (0..total_lines) to insert after. 0 = prepend; total_lines = append.")] int after_line,
        [Description("Text to insert. May span multiple lines. No automatic newline is added.")] string insert_text,
        CancellationToken cancellationToken) =>
        insertAfterLine.HandleAsync(new InsertIntentTextAfterLineCommand(intent_id, expected_version, after_line, insert_text), cancellationToken);

    [McpServerTool(Name = "search_intent_text", ReadOnly = true, UseStructuredContent = true)]
    [Description("Case-sensitive substring search over Intent.text. Returns up to limit matches with context_lines around each. Server hard cap on limit: 50. If total matches exceed limit, total_matches_estimate hints to refine the query.")]
    public Task<TextSearchResult> SearchIntentText(
        [Description("Intent identifier.")] string intent_id,
        [Description("Substring to find. Case-sensitive, byte-exact.")] string query,
        [Description("Lines of context around each match. Default: 3.")] int? context_lines = null,
        [Description("Max matches to return. Default: 10. Capped at 50.")] int? limit = null,
        CancellationToken cancellationToken = default) =>
        search.HandleAsync(new SearchIntentTextQuery(intent_id, query, context_lines, limit), cancellationToken);

    [McpServerTool(Name = "add_intent_qa", UseStructuredContent = true)]
    [Description("Append a question/answer pair to Intent training data (intent_qa). Does NOT increment current_version, but expected_version must match. Errors: intent.version_conflict, intent.not_found.")]
    public Task<Ack> AddIntentQa(
        [Description("Intent identifier.")] string intent_id,
        [Description("Expected current_version of the Intent.")] int expected_version,
        [Description("Question asked during interview.")] string question,
        [Description("User-provided answer.")] string answer,
        CancellationToken cancellationToken) =>
        addQa.HandleAsync(new AddIntentQaCommand(intent_id, expected_version, question, answer), cancellationToken);

    [McpServerTool(Name = "add_intent_review", UseStructuredContent = true)]
    [Description("Append a review note to Intent training data (intent_review). Does NOT increment current_version, but expected_version must match. Errors: intent.version_conflict, intent.not_found.")]
    public Task<Ack> AddIntentReview(
        [Description("Intent identifier.")] string intent_id,
        [Description("Expected current_version of the Intent.")] int expected_version,
        [Description("Review note: what the user wants corrected.")] string note,
        [Description("Reason: why this matters / what AI misunderstood.")] string reason,
        CancellationToken cancellationToken) =>
        addReview.HandleAsync(new AddIntentReviewCommand(intent_id, expected_version, note, reason), cancellationToken);

    [McpServerTool(Name = "get_instruction_bundle", ReadOnly = true, UseStructuredContent = true)]
    [Description("Read the instruction bundle for a work mode. Pass intent_id when an Intent is already known so audit can link instruction versions to it.")]
    public Task<InstructionBundle> GetInstructionBundle(
        [Description("Mode: interview, light_work, or new_project. /treview uses light_work.")] string mode,
        [Description("Optional Intent identifier this bundle will be used for. Omit before the Intent is created.")] string? intent_id,
        CancellationToken cancellationToken) =>
        getInstructionBundle.HandleAsync(new GetInstructionBundleQuery(mode, intent_id), cancellationToken);
}
