// MCP wire-format requires snake_case parameter names; tools are an API boundary.
#pragma warning disable CA1707
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Throne.Application.Instructions;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
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
    SetIntentStatusHandler setStatus,
    GetInstructionBundleHandler getInstructionBundle,
    IIntentAttachmentRepository attachments)
{
    private static readonly JsonSerializerOptions ToolJsonOptions = new(JsonSerializerDefaults.Web);

    [McpServerTool(Name = "create_intent", UseStructuredContent = true)]
    [Description("Create a new Intent with canonical text version v1. Use when no active Intent exists or the user explicitly starts a new one.")]
    public Task<Intent> CreateIntent(
        [Description("Initial canonical Intent.text. Must be non-empty and contain the user's actual intent, not a summary of tool usage.")] string text,
        [Description("Optional search/grouping tags. Prefer the current repo/project name first when known; pass an empty array when no tag is reliable.")] IReadOnlyList<string>? tags = null,
        CancellationToken cancellationToken = default) =>
        create.HandleAsync(new CreateIntentCommand(text, tags, TextVersionAuthor.Agent), cancellationToken);

    [McpServerTool(Name = "get_intent", ReadOnly = true)]
    [Description("Read canonical Intent state by id, including full text and attachments. Image attachments are returned as MCP image content blocks so agents can inspect them.")]
    public async Task<CallToolResult> GetIntent(
        [Description("Intent id returned by create_intent or supplied by the user.")] string intent_id,
        CancellationToken cancellationToken)
    {
        var intent = await get.HandleAsync(new GetIntentQuery(intent_id), cancellationToken).ConfigureAwait(false);
        var attachmentList = await attachments
            .ListByIntentAsync(intent.Id, cancellationToken)
            .ConfigureAwait(false);

        var content = new List<ContentBlock>();
        var imageReturned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var attachment in attachmentList)
        {
            if (!attachment.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var opened = await attachments
                .OpenContentAsync(intent.Id, attachment.Id, cancellationToken)
                .ConfigureAwait(false);
            if (opened is null)
            {
                continue;
            }

            await using var stream = opened.Content;
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
            content.Add(new ImageContentBlock
            {
                Data = Convert.ToBase64String(memory.ToArray()),
                MimeType = opened.Attachment.ContentType,
            });
            imageReturned.Add(attachment.Id);
        }

        var result = new McpIntentReadResult(
            intent.Id.Value,
            intent.Text,
            intent.Status,
            intent.CurrentVersion,
            intent.Tags,
            intent.CreatedAt,
            intent.UpdatedAt,
            attachmentList
                .Select(a => new McpIntentAttachmentReadResult(
                    a.Id,
                    a.IntentId,
                    a.FileName,
                    a.ContentType,
                    a.SizeBytes,
                    a.CreatedAt,
                    imageReturned.Contains(a.Id)))
                .ToArray(),
            imageReturned.Count);

        content.Insert(0, new TextContentBlock { Text = JsonSerializer.Serialize(result, ToolJsonOptions) });

        return new CallToolResult
        {
            Content = content,
            StructuredContent = JsonSerializer.SerializeToNode(result, ToolJsonOptions)?.AsObject(),
            IsError = false,
        };
    }

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

    [McpServerTool(Name = "add_intent_qa", UseStructuredContent = true)]
    [Description("Append one interview question/answer pair to training-only intent_qa. Does not change Intent.text or increment current_version.")]
    public Task<Ack> AddIntentQa(
        [Description("Intent id the interview step belongs to.")] string intent_id,
        [Description("current_version observed before recording this qa pair. Must still match the Intent.")] int expected_version,
        [Description("The exact useful question the agent asked the user.")] string question,
        [Description("The user's answer, preserving the substance needed for future training.")] string answer,
        CancellationToken cancellationToken) =>
        addQa.HandleAsync(new AddIntentQaCommand(intent_id, expected_version, question, answer), cancellationToken);

    [McpServerTool(Name = "add_intent_review", UseStructuredContent = true)]
    [Description("Append one post-work review note to training-only intent_review. Use before continuing fixes from /tfix. Does not increment current_version.")]
    public Task<Ack> AddIntentReview(
        [Description("Intent id the review belongs to.")] string intent_id,
        [Description("current_version observed before recording this review. Must still match the Intent.")] int expected_version,
        [Description("The user's concrete correction or complaint.")] string note,
        [Description("Why the correction matters, or what the agent misunderstood. Keep it concise and diagnostic.")] string reason,
        CancellationToken cancellationToken) =>
        addReview.HandleAsync(new AddIntentReviewCommand(intent_id, expected_version, note, reason), cancellationToken);

    [McpServerTool(Name = "get_instruction_bundle", UseStructuredContent = true)]
    [Description("Read the complete instruction bundle for a runtime mode. Call before interview/work and pass intent_id once known for audit linkage.")]
    public Task<InstructionBundle> GetInstructionBundle(
        [Description("Runtime mode: interview, light_work, new_project, or dream. Use light_work for /tfix continuation.")] string mode,
        [Description("Optional Intent id this bundle will govern. Omit only before the Intent exists.")] string? intent_id = null,
        CancellationToken cancellationToken = default) =>
        getInstructionBundle.HandleAsync(new GetInstructionBundleQuery(mode, intent_id), cancellationToken);

    [McpServerTool(Name = "mark_ready_for_review", UseStructuredContent = true)]
    [Description("Mark an Intent as ready_for_review after the agent completes a meaningful work pass and needs user attention.")]
    public Task<Intent> MarkReadyForReview(
        [Description("Intent id to move into ready_for_review.")] string intent_id,
        CancellationToken cancellationToken = default) =>
        setStatus.HandleAsync(
            new SetIntentStatusCommand(
                intent_id,
                IntentStatusNames.ReadyForReview,
                RejectReason: null,
                IntentTrainingAuthor.Agent,
                Source: "mark_ready_for_review"),
            cancellationToken);
}
