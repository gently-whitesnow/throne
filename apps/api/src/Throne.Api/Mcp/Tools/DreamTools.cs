using System.ComponentModel;
using ModelContextProtocol.Server;
using Throne.Application.Dreams;
using Throne.Domain.Dreams;

namespace Throne.Api.Mcp.Tools;

/// <summary>
/// MCP surface for DreamSession (ADR-0022). Three tools:
///   * <c>get_dream_sources</c> — vendor → path/hint map for where the agent
///     should look for prior conversations locally;
///   * <c>list_dream_sessions</c> — owner-scoped list of past /dream passes
///     (memory of summary / reflection / proposed_patch_ids / processed_ids);
///   * <c>record_dream_session</c> — appended at the end of a pass with the
///     summary, reflection and proposed_patch_ids it produced.
///
/// There is no apply / edit / reject on dream sessions — they are immutable
/// once recorded. Patch apply / reject lives on PromptPartPatch (user action,
/// HTTP-only).
/// </summary>
[McpServerToolType]
public sealed class DreamTools(
    GetDreamSourcesHandler sourcesHandler,
    ListDreamSessionsHandler listHandler,
    RecordDreamSessionHandler recordHandler)
{
    [McpServerTool(Name = "get_dream_sources", ReadOnly = true, UseStructuredContent = true)]
    [Description("Read the manifest list of vendor → filesystem path pairs the frontier agent should crawl for prior conversations during a /dream pass. The server never reads the dialog bytes itself; this tool only tells the agent WHERE to look. Sources are global in v1 (no per-user override). If the list is empty, no /dream sources are configured for this deployment.")]
    public Task<McpDreamSourcesResult> GetDreamSources(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var entries = sourcesHandler.Handle();
        var items = entries
            .Select(e => new McpDreamSourceEntry(e.Vendor, e.Path, e.Hint))
            .ToList();
        return Task.FromResult(new McpDreamSourcesResult(items));
    }

    [McpServerTool(Name = "list_dream_sessions", ReadOnly = true, UseStructuredContent = true)]
    [Description("List DreamSessions owned by the caller, ordered by created_at descending. Use this at the start of a /dream pass to recall prior summaries / reflections / proposed_patch_ids and to find processed_conversation_ids that should not be re-analysed. Pagination is opaque-cursor based. Empty `items` is a valid success state — it just means the caller has never recorded a dream pass yet; do NOT treat it as an error. Pass `host=<your hostname>` to scope the frontier to the current machine — without it the response mixes sessions recorded from every machine the owner has ever used.")]
    public async Task<McpDreamSessionListResult> ListDreamSessions(
        [Description("Optional vendor filter (e.g. claude-code, claude-desktop, codex-cli).")] string? vendor = null,
        [Description("Optional machine-hostname filter. Dream agents must pass their own hostname so the per-machine processed_conversation_ids frontier stays separated. Omit only when you genuinely want every machine's history (UI / analytics).")] string? host = null,
        [Description("Page size, default 20, capped at 100.")] int? limit = null,
        [Description("Opaque cursor returned as next_cursor by the previous page.")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var page = await listHandler.HandleAsync(
            new ListDreamSessionsQuery(vendor, host, limit, cursor),
            cancellationToken);
        return new McpDreamSessionListResult(
            page.Items.Select(DreamSessionMcpMapper.ToReadModel).ToList(),
            page.NextCursor);
    }

    [McpServerTool(Name = "record_dream_session", UseStructuredContent = true)]
    [Description("Append a DreamSession after a completed /dream pass. Records are immutable — there is no edit / delete. processed_conversation_ids must list every dialog id/path the agent actually read so the next pass skips them. proposed_patch_ids should list every PromptPartPatch the agent created during this pass. Even if nothing useful was found, still record a session with an empty proposed_patch_ids and a one-line summary explaining why.")]
    public async Task<McpDreamSessionReadModel> RecordDreamSession(
        [Description("Vendor whose conversations were analysed; must be one of the entries returned by get_dream_sources.")] string vendor,
        [Description("Hostname of the machine that ran this /dream pass (typically `os.hostname()` / `hostname` shell command). Required — the server scopes the per-machine processed_conversation_ids frontier by this value. Max 255 chars, non-empty.")] string host,
        [Description("Opaque agent-side ids / paths of dialogs read this pass. Up to 500 entries.")] IReadOnlyList<string> processed_conversation_ids,
        [Description("3-7 line summary of what was found and proposed in this pass. ≤4000 chars.")] string summary,
        [Description("Inclusive lower bound of the analysed period (RFC 3339); null when the agent took the full history.")] DateTimeOffset? date_from = null,
        [Description("Inclusive upper bound of the analysed period (RFC 3339); null when open-ended.")] DateTimeOffset? date_to = null,
        [Description("Agent's notes on prior applied patches (whether they landed in practice). Optional, ≤4000 chars.")] string? reflection = null,
        [Description("PromptPartPatch ids created during this pass (opaque to the server). Up to 50.")] IReadOnlyList<string>? proposed_patch_ids = null,
        CancellationToken cancellationToken = default)
    {
        var session = await recordHandler.HandleAsync(
            new RecordDreamSessionCommand(
                vendor,
                host,
                date_from,
                date_to,
                processed_conversation_ids ?? Array.Empty<string>(),
                summary,
                reflection,
                proposed_patch_ids ?? Array.Empty<string>()),
            cancellationToken);
        return DreamSessionMcpMapper.ToReadModel(session);
    }
}

internal static class DreamSessionMcpMapper
{
    public static McpDreamSessionReadModel ToReadModel(DreamSession session) => new(
        session.Id,
        session.Identity.CreatedAt,
        session.Payload.Vendor,
        session.Payload.Host,
        session.Payload.DateFrom,
        session.Payload.DateTo,
        session.Payload.ProcessedConversationIds.ToList(),
        session.Payload.Summary,
        session.Payload.Reflection,
        session.Payload.ProposedPatchIds.ToList());
}

public sealed record McpDreamSessionListResult(
    [property: Description("Page of dream sessions, newest first.")] IReadOnlyList<McpDreamSessionReadModel> Items,
    [property: Description("Opaque continuation token; null when the page exhausted the result set.")] string? NextCursor);

public sealed record McpDreamSessionReadModel(
    [property: Description("DreamSession id (32 hex chars).")] string Id,
    [property: Description("Creation timestamp (UTC).")] DateTimeOffset CreatedAt,
    [property: Description("Vendor whose dialogs were analysed.")] string Vendor,
    [property: Description("Machine hostname the /dream pass ran on. Null only for legacy records written before the field existed.")] string? Host,
    [property: Description("Inclusive lower bound of the analysed period; null when full history.")] DateTimeOffset? DateFrom,
    [property: Description("Inclusive upper bound of the analysed period; null when open-ended.")] DateTimeOffset? DateTo,
    [property: Description("Opaque dialog ids / paths the agent read in this pass.")] IReadOnlyList<string> ProcessedConversationIds,
    [property: Description("3-7 line summary of findings and proposals.")] string Summary,
    [property: Description("Agent's reflection on prior applied patches.")] string? Reflection,
    [property: Description("PromptPartPatch ids created during this pass.")] IReadOnlyList<string> ProposedPatchIds);

public sealed record McpDreamSourcesResult(
    [property: Description("Vendor / path / hint triples; empty when no sources are configured.")] IReadOnlyList<McpDreamSourceEntry> Items);

public sealed record McpDreamSourceEntry(
    [property: Description("Vendor identifier (e.g. claude-code, claude-desktop, codex-cli).")] string Vendor,
    [property: Description("Filesystem path where the vendor stores its conversations (may use '~').")] string Path,
    [property: Description("Free-form hint about the on-disk layout the agent should expect.")] string Hint);
