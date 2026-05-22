using System.ComponentModel;
using ModelContextProtocol.Server;
using Throne.Application.Instructions;
using Throne.Application.Intents;
using Throne.Application.Intents.Attachments;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.TextVersions;

namespace Throne.Api.Mcp.Tools;

[McpServerToolType]
public sealed class IntentTools(
    CreateIntentHandler create,
    GetIntentHandler get,
    GetInstructionBundleHandler getInstructionBundle,
    ListIntentsHandler listIntents,
    MoveIntentHandler moveIntentHandler,
    IIntentLinkRepository linkRepository,
    IIntentAttachmentRepository attachments,
    IntentToolTagRefs tagRefs)
{
    [McpServerTool(Name = "create_intent", UseStructuredContent = true)]
    [Description("Create a new Intent with canonical text version v1. Use when no active Intent exists or the user explicitly starts a new one. If the new Intent arises in the context of another active Intent, consider linking them via link_intent(from_id=new, to_id=source, type=\"derived_from\") — or type=\"relates\" for a thematic connection — so the new Intent does not become an orphan. Returns a compact ack; re-read with get_intent if the full body is needed.")]
    public async Task<McpWriteAck> CreateIntent(
        [Description("Initial canonical Intent.text. Must be non-empty and contain the user's actual intent, not a summary of tool usage.")] string text,
        [Description("Tag names to attach. Pass exactly one entry — the normalized name of the current repository/project (e.g. 'throne'). The server upserts tags by name (existing tag → reused id, new tag → created and linked). Do not pass thematic / technological / feature tags.")] IReadOnlyList<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var intent = await create.HandleAsync(new CreateIntentCommand(text, tags, TextVersionAuthor.Agent), cancellationToken);
        return new McpWriteAck(intent.Id.Value, intent.State.CurrentVersion, Accepted: true);
    }

    [McpServerTool(Name = "list_intents", ReadOnly = true, UseStructuredContent = true)]
    [Description("List intents owned by the current user with compact previews. Use to discover intent ids before calling get_intent. Filters: tag (slug), status (multi), query (case-insensitive substring of Intent.text). Sort defaults to updated_desc; pagination via opaque next_cursor.")]
    // NOTE: must return McpIntentListResult (the payload type), NOT CallToolResult.
    // When UseStructuredContent=true the MCP SDK derives the tool's outputSchema from
    // the return type. CallToolResult serialises with a top-level `structuredContent: true`
    // (any-schema), which Zod inside the Claude Code app rejects with
    // "tools[*].outputSchema.properties.structuredContent: Invalid input" — that error
    // discards the entire tools/list response and the user sees zero tools from this
    // server. See incident: master HEAD around 278f8c3. Errors are surfaced by throwing;
    // the SDK wraps them into a CallToolResult with IsError=true.
    public async Task<McpIntentListResult> ListIntents(
        [Description("Tag slug to filter by. The agent should pass the current repository slug here when scoping to one project; omit for cross-project listing.")] string? tag = null,
        [Description("Statuses to include. Pass values like 'draft', 'interview', 'ready_for_work', 'work', 'ready_for_review', 'needs_help', 'done', 'reject', 'fridge'. Omit for all statuses. To list 'archive', pass ['done','reject']. 'needs_help' marks intents where the agent is blocked and asks the operator; 'fridge' is the user-only «later» bucket.")] IReadOnlyList<string>? status = null,
        [Description("Case-insensitive substring of Intent.text. Omit to skip text filtering.")] string? query = null,
        [Description("Sort order: 'sort_key_asc' (default — user-defined order via fractional sort_key), 'updated_desc', 'created_desc', or 'created_asc'.")] string? sort = null,
        [Description("Page size, default 50, capped at 100.")] int? limit = null,
        [Description("Opaque cursor returned as next_cursor by the previous page. Omit for the first page.")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var page = await listIntents.HandleAsync(
            new ListIntentsPagedQuery(
                Statuses: status is { Count: > 0 } ? status : null,
                TagName: tag,
                Query: query,
                Sort: IntentListSortParser.Parse(sort),
                Limit: limit ?? ListIntentsHandler.DefaultLimit,
                Cursor: IntentListCursorCodec.Parse(cursor)),
            cancellationToken);

        var uniqueTagIds = page.Items
            .SelectMany(i => i.TagIds)
            .GroupBy(t => t.Value, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();
        var tagsById = (await tagRefs.BuildAsync(uniqueTagIds, cancellationToken))
            .ToDictionary(t => t.Id, t => t, StringComparer.Ordinal);

        var items = page.Items.Select(i => IntentListItemMapper.ToItem(i, tagsById)).ToList();
        return new McpIntentListResult(items, IntentListCursorCodec.Encode(page.NextCursor));
    }

    [McpServerTool(Name = "get_intent", ReadOnly = true)]
    [Description("Read canonical Intent state by id, including full text and attachment metadata. Attachment bytes are NOT inlined. For each attachment call the tool named in 'recommended_tool' (read_intent_attachment_image for images, read_intent_attachment_text for text/log files). The response also carries 'links' — outgoing + incoming graph edges with the peer intent inlined as a compact preview; agent should inspect 'links' before acting on dependencies (e.g. honour 'incoming blocks' as 'blocked_by').")]
    public async Task<McpToolPayload> GetIntent(
        [Description("Intent id returned by create_intent or supplied by the user.")] string intent_id,
        CancellationToken cancellationToken)
    {
        var intent = await get.HandleAsync(new GetIntentQuery(intent_id), cancellationToken);
        var attachmentList = await attachments.ListByIntentAsync(intent.Id, cancellationToken);
        var links = await linkRepository.ListByIntentAsync(intent.Id, cancellationToken);
        var tagsById = await tagRefs.BuildIntentReadMapAsync(intent, links, cancellationToken);

        var result = IntentReadResultBuilder.Build(intent, attachmentList, links, tagsById);
        return IntentReadResultRenderer.Render(result);
    }

    [McpServerTool(Name = "get_instruction_bundle")]
    [Description("Read the complete instruction bundle for a runtime mode. Pass intent_id once known so the server can transition the Intent's status automatically (interview/work/fix bundles drive transitions on read).")]
    public async Task<McpToolPayload> GetInstructionBundle(
        [Description("Runtime mode: interview, work, fix, dream, or transfer. Pick by user intent — see the mini-router instructions returned at MCP initialize.")] string mode,
        [Description("Optional Intent id this bundle will govern. Omit only before the Intent exists.")] string? intent_id = null,
        CancellationToken cancellationToken = default)
    {
        var bundle = await getInstructionBundle.HandleAsync(new GetInstructionBundleQuery(mode, intent_id), cancellationToken);
        return InstructionBundleRenderer.Render(bundle);
    }

    [McpServerTool(Name = "move_intent", UseStructuredContent = true)]
    [Description("Reorder an intent in the user-defined sort order. Supply at least one of before_id (predecessor) or after_id (successor); supplying both pins the intent strictly between them. The server reads the pivots' sort keys and computes the midpoint — the agent never sends keys.")]
    public Task<Intent> MoveIntent(
        [Description("Intent id to move.")] string intent_id,
        [Description("Id of the intent that should immediately precede the moved intent. Optional when after_id is supplied.")] string? before_id = null,
        [Description("Id of the intent that should immediately follow the moved intent. Optional when before_id is supplied.")] string? after_id = null,
        CancellationToken cancellationToken = default) =>
        moveIntentHandler.HandleAsync(new MoveIntentCommand(intent_id, before_id, after_id), cancellationToken);
}
