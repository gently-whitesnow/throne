using ModelContextProtocol.Protocol;

namespace Throne.Api.Mcp.Tools;

/// <summary>
/// OOB envelope returned by prompt-like MCP tools. ADR-0003 §8.1 (2026-05 amendment)
/// requires <see cref="CallToolResult.StructuredContent"/> to be <c>null</c> on the
/// wire for these tools — any non-empty StructuredContent makes structured-aware
/// clients (Claude Code) hide <c>Content[]</c> from the model, regressing to the
/// incident behind intents 9cc71a8c… and 6e96cd22….
///
/// Compact refs still need to reach the audit channel (<c>mcp_call_log.result_summary</c>).
/// The renderer puts them in <see cref="AuditSummary"/>; <see cref="AuditingMcpServerTool"/>
/// unpacks the envelope, sends <see cref="Wire"/> over the wire as-is, and forwards
/// <see cref="AuditSummary"/> directly to <see cref="McpAuditLogger.WriteFromCallResultAsync"/>
/// — no re-parsing of wire payload.
/// </summary>
// Must be public because [McpServerToolType] tools are public and C# requires the
// return type to be at least as accessible as the method. The intent is internal
// (this is a server-side envelope, not a domain contract) — keep callers inside
// Throne.Api / Throne.Api.Tests only.
public sealed record McpToolPayload(
    CallToolResult Wire,
    IReadOnlyDictionary<string, object?>? AuditSummary);
