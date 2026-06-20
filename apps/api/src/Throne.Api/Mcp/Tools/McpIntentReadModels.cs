using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Throne.Api.Mcp.Tools;

public sealed record McpIntentReadResult(
    [property: Description("Intent identifier.")] string Id,
    [property: Description("Full canonical Intent.text.")] string Text,
    [property: Description("Current intent status.")] string Status,
    [property: Description("Current text version.")] int CurrentVersion,
    [property: Description("Tags currently attached to the intent.")] IReadOnlyList<McpTagRef> Tags,
    [property: Description("Fractional sort key (base62). Use to reason about the intent's position in the user-defined order.")] string SortKey,
    [property: Description("Creation timestamp.")] DateTimeOffset CreatedAt,
    [property: Description("Last update timestamp.")] DateTimeOffset UpdatedAt,
    [property: Description("Attachment metadata. Bytes are NOT inlined; for each entry pick the attachment-read MCP tool that matches its 'kind' (image vs text/log) from your own registered tool list, passing this intent's id and the attachment id.")]
    IReadOnlyList<McpIntentAttachmentReadResult> Attachments,
    [property: Description("Outgoing + incoming graph edges incident to this intent. Each edge has one forward direction and a blocking flag.")]
    IReadOnlyList<McpIntentLinkRead> Links,
    [property: Description("Repository bindings attached to this intent. Empty when the intent has no bindings. The agent uses 'workspace_path' as the local working tree; when 'pull_request_number' is present the agent reads review comments directly via `gh api repos/{owner}/{repo}/pulls/{n}/comments` — Throne does not cache comment bodies.")]
    IReadOnlyList<McpIntentRepositoryRef> Repositories);

public sealed record McpIntentRepositoryRef(
    [property: Description("Binding identifier (used as binding_id in subsequent MCP/HTTP calls).")] string BindingId,
    [property: Description("Git provider wire name, e.g. 'github'.")] string Provider,
    [property: Description("Repository owner / org login.")] string Owner,
    [property: Description("Repository name without owner prefix.")] string Repo,
    [property: Description("Default branch of the upstream repository.")] string DefaultBranch,
    [property: Description("Absolute path of the local clone (see ADR-0024 workspace layout). Use this as the agent's working directory.")] string WorkspacePath,
    [property: Description("Clone status: 'pending' | 'cloning' | 'ready' | 'failed' | 'broken'. Only 'ready' is safe to operate on.")] string CloneStatus,
    [property: Description("Pull request number attached to the binding when present; null otherwise.")] int? PullRequestNumber,
    [property: Description("Pull request state: 'open' | 'closed' | 'merged' when a PR is attached; null otherwise.")] string? PullRequestState);

public sealed record McpIntentLinkRead(
    [property: Description("Edge identifier.")] string Id,
    [property: Description("Direction relative to the queried intent: 'outgoing' = this intent is the from_id, 'incoming' = this intent is the to_id.")] string Direction,
    [property: Description("True when this edge is a hard dependency/blocking edge.")] bool Blocking,
    [property: Description("Authored by 'user' or 'agent'.")] string Author,
    [property: Description("Optional rationale string supplied at link creation; null if absent.")] string? Rationale,
    [property: Description("Creation timestamp.")] DateTimeOffset CreatedAt,
    [property: Description("The peer intent — outgoing direction targets it, incoming direction originates from it.")] McpIntentLinkPeer Peer);

public sealed record McpIntentLinkPeer(
    [property: Description("Peer intent identifier.")] string Id,
    [property: Description("Peer intent status.")] string Status,
    [property: Description("Peer intent current text version.")] int CurrentVersion,
    [property: Description("Peer intent fractional sort key.")] string SortKey,
    [property: Description("First non-empty line of the peer's text, trimmed to 200 characters.")] string Preview,
    [property: Description("Tags attached to the peer intent.")] IReadOnlyList<McpTagRef> Tags);

public sealed record McpIntentLinkResult(
    [property: Description("Edge identifier.")] string Id,
    [property: Description("Source intent id.")] string FromId,
    [property: Description("Target intent id.")] string ToId,
    [property: Description("True when this edge is a hard dependency/blocking edge.")] bool Blocking,
    [property: Description("Authored by 'user' or 'agent'.")] string Author,
    [property: Description("Optional rationale string; null if absent.")] string? Rationale,
    [property: Description("Creation timestamp.")] DateTimeOffset CreatedAt);

public sealed record McpIntentUnlinkResult(
    [property: Description("True when an edge was actually deleted; false when the edge was already absent (idempotent retry).")] bool Deleted);

public sealed record McpIntentLinksPageResult(
    [property: Description("Edges incident to the queried intent.")] IReadOnlyList<McpIntentLinkRead> Items,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    [property: Description("Opaque cursor to fetch the next page; null when this is the last page.")] string? NextCursor);

public sealed record McpIntentListResult(
    [property: Description("Compact list of intents matching the filter.")] IReadOnlyList<McpIntentListItem> Items,
    [property: Description("Opaque cursor to fetch the next page; null when this is the last page.")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    string? NextCursor);

public sealed record McpIntentListItem(
    [property: Description("Intent identifier.")] string Id,
    [property: Description("Current intent status.")] string Status,
    [property: Description("Current text version. Use as expected_version for write tools.")] int CurrentVersion,
    [property: Description("Tags currently attached to the intent.")] IReadOnlyList<McpTagRef> Tags,
    [property: Description("First non-empty line of Intent.text, trimmed to 200 characters.")] string Preview,
    [property: Description("Fractional sort key (base62). Lower lexicographic value = higher in the list.")] string SortKey,
    [property: Description("Creation timestamp.")] DateTimeOffset CreatedAt,
    [property: Description("Last update timestamp.")] DateTimeOffset UpdatedAt);

public sealed record McpTagRef(
    [property: Description("Tag identifier.")] string Id,
    [property: Description("Normalized hashtag-shaped slug.")] string Name);

public sealed record McpIntentAttachmentReadResult(
    [property: Description("Attachment identifier.")] string Id,
    [property: Description("Owning intent identifier.")] string IntentId,
    [property: Description("Original file name.")] string FileName,
    [property: Description("Stored MIME type. Image attachments are server-compressed to image/jpeg.")] string ContentType,
    [property: Description("Stored size in bytes (post-compression for images).")] long SizeBytes,
    [property: Description("Upload timestamp.")] DateTimeOffset CreatedAt,
    [property: Description("Content family: 'image', 'text' or 'unsupported'. Drives the choice of read tool.")] string Kind,
    [property: Description("Logical (unprefixed) name of the MCP tool that returns the bytes for this attachment, or null if unsupported. Your client registers it under its own server prefix (e.g. mcp__throne__… or throne_…); select the matching tool from your tool list rather than calling this bare name.")] string? RecommendedTool,
    [property: Description("True for image attachments that have been server-side downscaled to ≤1024 px JPEG q75.")] bool IsCompressedImage,
    [property: Description("Width in pixels of the stored image (post-compression). Null for non-image attachments.")] int? CompressedWidth,
    [property: Description("Height in pixels of the stored image (post-compression). Null for non-image attachments.")] int? CompressedHeight);
