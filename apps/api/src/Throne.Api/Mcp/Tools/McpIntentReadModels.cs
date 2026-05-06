using System.ComponentModel;

namespace Throne.Api.Mcp.Tools;

public sealed record McpIntentReadResult(
    [property: Description("Intent identifier.")] string Id,
    [property: Description("Full canonical Intent.text.")] string Text,
    [property: Description("Current intent status.")] string Status,
    [property: Description("Current text version.")] int CurrentVersion,
    [property: Description("Tags currently attached to the intent.")] IReadOnlyList<McpTagRef> Tags,
    [property: Description("Creation timestamp.")] DateTimeOffset CreatedAt,
    [property: Description("Last update timestamp.")] DateTimeOffset UpdatedAt,
    [property: Description("Attachment metadata. Fetch binary content via the MCP resource 'intent://{intent_id}/attachments'.")]
    IReadOnlyList<McpIntentAttachmentReadResult> Attachments);

public sealed record McpTagRef(
    [property: Description("Tag identifier.")] string Id,
    [property: Description("Normalized hashtag-shaped slug.")] string Name);

public sealed record McpIntentAttachmentReadResult(
    [property: Description("Attachment identifier.")] string Id,
    [property: Description("Owning intent identifier.")] string IntentId,
    [property: Description("Original file name.")] string FileName,
    [property: Description("Declared MIME type.")] string ContentType,
    [property: Description("Stored size in bytes.")] long SizeBytes,
    [property: Description("Upload timestamp.")] DateTimeOffset CreatedAt);
