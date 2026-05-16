using System.ComponentModel;
using ModelContextProtocol.Server;
using Throne.Application.Intents.Linking;
using Throne.Application.Ports;
using Throne.Domain.Intents.Linking;

namespace Throne.Api.Mcp.Tools;

[McpServerToolType]
public sealed class IntentLinkTools(
    LinkIntentHandler linkIntentHandler,
    UnlinkIntentHandler unlinkIntentHandler,
    ListIntentLinksHandler listIntentLinksHandler,
    ITagRepository tagRepository)
{
    [McpServerTool(Name = "link_intent", UseStructuredContent = true)]
    [Description("Create one directed edge between two intents in the M:N graph. Stage 1 supports 'relates' (thematic), 'blocks' (dependency), 'derived_from' (causal trace). Mirror roles ('blocked_by', 'source_of') are not separate types — they are inferred from incoming edges via get_intent.links. Self-links and 'duplicate_of' (reserved for stage 3) are rejected. Duplicate (from_id, to_id, type) edges are rejected with 'link.duplicate'.")]
    public async Task<McpIntentLinkResult> LinkIntent(
        [Description("Source intent id.")] string from_id,
        [Description("Target intent id.")] string to_id,
        [Description("Edge type: 'relates' | 'blocks' | 'derived_from'.")] string type,
        [Description("Optional rationale string (≤ 1000 chars). Surface in UI activity feeds; agent should explain non-obvious blocks/derived_from.")] string? rationale = null,
        CancellationToken cancellationToken = default)
    {
        var link = await linkIntentHandler.HandleAsync(
            new LinkIntentCommand(from_id, to_id, type, IntentLinkAuthor.Agent, rationale),
            cancellationToken);
        return ToMcpLinkResult(link);
    }

    [McpServerTool(Name = "unlink_intent", UseStructuredContent = true)]
    [Description("Delete one directed edge by its natural key (from_id, to_id, type). Idempotent: a missing edge is treated as success. Mirror roles cannot be deleted directly — delete the underlying outgoing edge from the originator.")]
    public async Task<McpIntentUnlinkResult> UnlinkIntent(
        [Description("Source intent id.")] string from_id,
        [Description("Target intent id.")] string to_id,
        [Description("Edge type: 'relates' | 'blocks' | 'derived_from'.")] string type,
        CancellationToken cancellationToken = default)
    {
        var deleted = await unlinkIntentHandler.HandleAsync(
            new UnlinkIntentCommand(from_id, to_id, type),
            cancellationToken);
        return new McpIntentUnlinkResult(deleted);
    }

    [McpServerTool(Name = "list_intent_links", ReadOnly = true, UseStructuredContent = true)]
    [Description("List edges incident to an intent. Use when get_intent.links would be paginated for a high-degree node, or to filter by direction/type. Default direction is 'both' (returns outgoing + incoming).")]
    public async Task<McpIntentLinksPageResult> ListIntentLinks(
        [Description("Intent id whose graph edges to list.")] string intent_id,
        [Description("Optional direction filter: 'outgoing' (this intent is from_id) | 'incoming' (this intent is to_id). Omit for both.")] string? direction = null,
        [Description("Optional type filter: 'relates' | 'blocks' | 'derived_from'.")] string? type = null,
        [Description("Page size, default 50, capped at 200.")] int? limit = null,
        [Description("Opaque cursor returned as next_cursor by the previous page.")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var dir = direction switch
        {
            null or "" => (IntentLinkDirection?)null,
            "outgoing" => IntentLinkDirection.Outgoing,
            "incoming" => IntentLinkDirection.Incoming,
            _ => throw new ArgumentException(
                $"Unknown direction '{direction}'. Allowed: outgoing, incoming.",
                nameof(direction)),
        };

        var page = await listIntentLinksHandler.HandleAsync(
            new ListIntentLinksQuery(intent_id, dir, type, limit ?? ListIntentLinksHandler.DefaultLimit, cursor),
            cancellationToken);

        var tagIds = page.Items.SelectMany(v => v.Other.TagIds).ToList();
        var tagRefs = await IntentToolHelpers.BuildTagRefsAsync(tagRepository, tagIds, cancellationToken);
        var tagsById = tagRefs
            .GroupBy(t => t.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var items = page.Items.Select(v => ToMcpLinkRead(v, tagsById)).ToList();
        return new McpIntentLinksPageResult(items, page.NextCursor);
    }

    internal static McpIntentLinkResult ToMcpLinkResult(IntentLink link) => new(
        link.Id,
        link.FromId.Value,
        link.ToId.Value,
        link.Type,
        link.Author.ToWire(),
        link.Rationale,
        link.CreatedAt);

    internal static McpIntentLinkRead ToMcpLinkRead(IntentLinkView view, Dictionary<string, McpTagRef> tagsById) => new(
        view.Link.Id,
        view.Direction == IntentLinkDirection.Outgoing ? "outgoing" : "incoming",
        view.Link.Type,
        view.Link.Author.ToWire(),
        view.Link.Rationale,
        view.Link.CreatedAt,
        new McpIntentLinkPeer(
            view.Other.Id.Value,
            view.Other.State.Status,
            view.Other.State.CurrentVersion,
            view.Other.State.SortKey,
            IntentToolHelpers.BuildPreview(view.Other.State.Text),
            view.Other.TagIds
                .Select(id => tagsById.TryGetValue(id.Value, out var t) ? t : null)
                .Where(t => t is not null)
                .Select(t => t!)
                .ToList()));
}
