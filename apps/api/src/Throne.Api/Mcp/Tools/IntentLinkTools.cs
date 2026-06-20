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
    IntentToolTagRefs tagRefs)
{
    [McpServerTool(Name = "link_intent", UseStructuredContent = true)]
    [Description("Create one directed edge between two intents in the M:N graph. Direction is always from cause/parent to consequence/child. Set blocking=true only for hard dependencies that block work. Self-links are rejected. Duplicate (from_id, to_id) edges are rejected with 'link.duplicate'.")]
    public async Task<McpIntentLinkResult> LinkIntent(
        [Description("Source intent id.")] string from_id,
        [Description("Target intent id.")] string to_id,
        [Description("True for hard dependency edges; false for soft context/provenance edges.")] bool blocking,
        [Description("Optional rationale string (≤ 1000 chars). Surface in UI activity feeds; agent should explain non-obvious graph edges.")] string? rationale = null,
        CancellationToken cancellationToken = default)
    {
        var link = await linkIntentHandler.HandleAsync(
            new LinkIntentCommand(from_id, to_id, blocking, IntentLinkAuthor.Agent, rationale),
            cancellationToken);
        return ToMcpLinkResult(link);
    }

    [McpServerTool(Name = "unlink_intent", UseStructuredContent = true)]
    [Description("Delete one directed edge by its natural key (from_id, to_id). Idempotent: a missing edge is treated as success.")]
    public async Task<McpIntentUnlinkResult> UnlinkIntent(
        [Description("Source intent id.")] string from_id,
        [Description("Target intent id.")] string to_id,
        CancellationToken cancellationToken = default)
    {
        var deleted = await unlinkIntentHandler.HandleAsync(
            new UnlinkIntentCommand(from_id, to_id),
            cancellationToken);
        return new McpIntentUnlinkResult(deleted);
    }

    [McpServerTool(Name = "list_intent_links", ReadOnly = true, UseStructuredContent = true)]
    [Description("List edges incident to an intent. Use when get_intent.links would be paginated for a high-degree node, or to filter by direction/blocking. Default direction is 'both' (returns outgoing + incoming).")]
    public async Task<McpIntentLinksPageResult> ListIntentLinks(
        [Description("Intent id whose graph edges to list.")] string intent_id,
        [Description("Optional direction filter: 'outgoing' (this intent is from_id) | 'incoming' (this intent is to_id). Omit for both.")] string? direction = null,
        [Description("Optional blocking filter. true = hard dependency edges, false = soft context/provenance edges.")] bool? blocking = null,
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
            new ListIntentLinksQuery(intent_id, dir, blocking, limit ?? ListIntentLinksHandler.DefaultLimit, cursor),
            cancellationToken);

        var tagIds = page.Items.SelectMany(v => v.Other.TagIds).ToList();
        var refs = await tagRefs.BuildAsync(tagIds, cancellationToken);
        var tagsById = refs
            .GroupBy(t => t.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var items = page.Items.Select(v => ToMcpLinkRead(v, tagsById)).ToList();
        return new McpIntentLinksPageResult(items, page.NextCursor);
    }

    internal static McpIntentLinkResult ToMcpLinkResult(IntentLink link) => new(
        link.Id,
        link.FromId.Value,
        link.ToId.Value,
        link.Blocking,
        link.Author.ToWire(),
        link.Rationale,
        link.CreatedAt);

    internal static McpIntentLinkRead ToMcpLinkRead(IntentLinkView view, Dictionary<string, McpTagRef> tagsById) => new(
        view.Link.Id,
        view.Direction == IntentLinkDirection.Outgoing ? "outgoing" : "incoming",
        view.Link.Blocking,
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
