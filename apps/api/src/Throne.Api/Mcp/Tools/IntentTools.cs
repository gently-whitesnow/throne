// MCP wire-format requires snake_case parameter names; tools are an API boundary.
#pragma warning disable CA1707
using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Throne.Application.Instructions;
using Throne.Application.Intents;
using Throne.Application.Intents.Attachments;
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
    SetIntentTagsHandler setTagsHandler,
    GetInstructionBundleHandler getInstructionBundle,
    ListIntentsHandler listIntents,
    IIntentAttachmentRepository attachments,
    ITagRepository tagRepository)
{
    private static readonly JsonSerializerOptions ToolJsonOptions = new(JsonSerializerDefaults.Web);

    private async Task<IReadOnlyList<McpTagRef>> BuildTagRefsAsync(IReadOnlyList<Throne.Domain.Tags.TagId> tagIds, CancellationToken ct)
    {
        if (tagIds.Count == 0)
        {
            return [];
        }

        var idSet = tagIds.Select(t => t.Value).ToHashSet(StringComparer.Ordinal);
        var all = await tagRepository.ListAllAsync(ct);
        var refs = new List<McpTagRef>(tagIds.Count);
        foreach (var id in tagIds)
        {
            var tag = all.FirstOrDefault(t => t.Id.Value == id.Value);
            if (tag is null)
            {
                continue;
            }
            refs.Add(new McpTagRef(tag.Id.Value, tag.Name));
        }
        return refs;
    }

    [McpServerTool(Name = "create_intent", UseStructuredContent = true)]
    [Description("Create a new Intent with canonical text version v1. Use when no active Intent exists or the user explicitly starts a new one.")]
    public Task<Intent> CreateIntent(
        [Description("Initial canonical Intent.text. Must be non-empty and contain the user's actual intent, not a summary of tool usage.")] string text,
        [Description("Tag names to attach. Pass exactly one entry — the normalized name of the current repository/project (e.g. 'throne'). The server upserts tags by name (existing tag → reused id, new tag → created and linked). Do not pass thematic / technological / feature tags.")] IReadOnlyList<string>? tags = null,
        CancellationToken cancellationToken = default) =>
        create.HandleAsync(new CreateIntentCommand(text, tags, TextVersionAuthor.Agent), cancellationToken);

    [McpServerTool(Name = "set_intent_tags", UseStructuredContent = true)]
    [Description("Replace the set of tags attached to an Intent using upsert-by-name. Pass tag names; existing tags are reused, missing tags are created.")]
    public Task<Intent> SetIntentTags(
        [Description("Intent id to mutate.")] string intent_id,
        [Description("current_version observed from the latest get_intent. Tag changes do not bump current_version but the value must still match.")] int expected_version,
        [Description("Tag names to attach (slug-style). Pass an empty array to detach all tags.")] IReadOnlyList<string> tags,
        CancellationToken cancellationToken = default) =>
        setTagsHandler.HandleAsync(
            new SetIntentTagsCommand(intent_id, expected_version, TagIds: null, TagNames: tags),
            cancellationToken);

    [McpServerTool(Name = "list_intents", ReadOnly = true, UseStructuredContent = true)]
    [Description("List intents owned by the current user with compact previews. Use to discover intent ids before calling get_intent. Filters: tag (slug), status (multi), query (case-insensitive substring of Intent.text). Sort defaults to updated_desc; pagination via opaque next_cursor.")]
    public async Task<McpIntentListResult> ListIntents(
        [Description("Tag slug to filter by. The agent should pass the current repository slug here when scoping to one project; omit for cross-project listing.")] string? tag = null,
        [Description("Statuses to include. Pass values like 'draft', 'ready_for_work', 'work', 'ready_for_review', 'done', 'reject'. Omit for all statuses. To list 'archive', pass ['done','reject'].")] IReadOnlyList<string>? status = null,
        [Description("Case-insensitive substring of Intent.text. Omit to skip text filtering.")] string? query = null,
        [Description("Sort order: 'updated_desc' (default), 'created_desc', or 'created_asc'.")] string? sort = null,
        [Description("Page size, default 50, capped at 100.")] int? limit = null,
        [Description("Opaque cursor returned as next_cursor by the previous page. Omit for the first page.")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var sortValue = ParseSort(sort);
        var cursorValue = ParseCursor(cursor);
        var statuses = status is { Count: > 0 } ? status : null;
        var effectiveLimit = limit ?? ListIntentsHandler.DefaultLimit;

        var page = await listIntents.HandleAsync(
            new ListIntentsPagedQuery(
                Statuses: statuses,
                TagName: tag,
                Query: query,
                Sort: sortValue,
                Limit: effectiveLimit,
                Cursor: cursorValue),
            cancellationToken);

        var uniqueTagIds = page.Items
            .SelectMany(i => i.TagIds)
            .GroupBy(t => t.Value, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();
        var tagRefs = await BuildTagRefsAsync(uniqueTagIds, cancellationToken);
        var tagsById = tagRefs.ToDictionary(t => t.Id, t => t, StringComparer.Ordinal);

        var items = new List<McpIntentListItem>(page.Items.Count);
        foreach (var intent in page.Items)
        {
            var intentTags = intent.TagIds
                .Select(id => tagsById.TryGetValue(id.Value, out var t) ? t : null)
                .Where(t => t is not null)
                .Select(t => t!)
                .ToList();
            items.Add(new McpIntentListItem(
                Id: intent.Id.Value,
                Status: intent.Status,
                CurrentVersion: intent.CurrentVersion,
                Tags: intentTags,
                Preview: BuildPreview(intent.Text),
                CreatedAt: intent.CreatedAt,
                UpdatedAt: intent.UpdatedAt));
        }

        return new McpIntentListResult(items, EncodeCursor(page.NextCursor));
    }

    private static IntentListSort ParseSort(string? raw) => raw switch
    {
        null or "" or "updated_desc" => IntentListSort.UpdatedDesc,
        "created_desc" => IntentListSort.CreatedDesc,
        "created_asc" => IntentListSort.CreatedAsc,
        _ => throw new ArgumentException(
            $"Unknown sort '{raw}'. Allowed: updated_desc, created_desc, created_asc.",
            nameof(raw)),
    };

    private static string BuildPreview(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }
            return trimmed.Length <= 200 ? trimmed : trimmed[..200];
        }
        return string.Empty;
    }

    private static string? EncodeCursor(IntentListCursor? cursor)
    {
        if (cursor is null)
        {
            return null;
        }
        var iso = cursor.SortValue.UtcDateTime.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
        var raw = $"{iso}|{cursor.Id}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));
    }

    private static IntentListCursor? ParseCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }
        string decoded;
        try
        {
            decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("cursor is not a valid base64 token.", nameof(cursor), ex);
        }
        var sep = decoded.IndexOf('|');
        if (sep <= 0 || sep == decoded.Length - 1)
        {
            throw new ArgumentException("cursor payload is malformed.", nameof(cursor));
        }
        var iso = decoded[..sep];
        var id = decoded[(sep + 1)..];
        if (!DateTimeOffset.TryParse(
                iso,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var sortValue))
        {
            throw new ArgumentException("cursor sort-value is not a valid timestamp.", nameof(cursor));
        }
        return new IntentListCursor(sortValue, id);
    }

    [McpServerTool(Name = "get_intent", ReadOnly = true)]
    [Description("Read canonical Intent state by id, including full text and attachment metadata. Attachment bytes are NOT inlined. For each attachment call the tool named in 'recommended_tool' (read_intent_attachment_image for images, read_intent_attachment_text for text/log files).")]
    public async Task<CallToolResult> GetIntent(
        [Description("Intent id returned by create_intent or supplied by the user.")] string intent_id,
        CancellationToken cancellationToken)
    {
        var intent = await get.HandleAsync(new GetIntentQuery(intent_id), cancellationToken);
        var attachmentList = await attachments
            .ListByIntentAsync(intent.Id, cancellationToken);

        var tagRefs = await BuildTagRefsAsync(intent.TagIds, cancellationToken);

        var result = new McpIntentReadResult(
            intent.Id.Value,
            intent.Text,
            intent.Status,
            intent.CurrentVersion,
            tagRefs,
            intent.CreatedAt,
            intent.UpdatedAt,
            attachmentList
                .Select(a =>
                {
                    var kind = AttachmentKindResolver.Resolve(a.ContentType);
                    return new McpIntentAttachmentReadResult(
                        a.Id,
                        a.IntentId,
                        a.FileName,
                        a.ContentType,
                        a.SizeBytes,
                        a.CreatedAt,
                        AttachmentKindResolver.KindWireName(kind),
                        AttachmentKindResolver.RecommendedTool(kind),
                        a.IsCompressed,
                        a.CompressedWidth,
                        a.CompressedHeight);
                })
                .ToArray());

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = JsonSerializer.Serialize(result, ToolJsonOptions) }],
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
        [Description("Runtime mode: interview, work, fix, dream, or transfer. Use fix for /tfix continuation, work for /twork, transfer when the user asks to send chat history with agents to Throne for training.")] string mode,
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

    [McpServerTool(Name = "mark_ready_for_work", UseStructuredContent = true)]
    [Description("Mark an Intent as ready_for_work after interview when the brief is clear enough to be picked up for execution.")]
    public Task<Intent> MarkReadyForWork(
        [Description("Intent id to move into ready_for_work.")] string intent_id,
        CancellationToken cancellationToken = default) =>
        setStatus.HandleAsync(
            new SetIntentStatusCommand(
                intent_id,
                IntentStatusNames.ReadyForWork,
                RejectReason: null,
                IntentTrainingAuthor.Agent,
                Source: "mark_ready_for_work"),
            cancellationToken);
}
