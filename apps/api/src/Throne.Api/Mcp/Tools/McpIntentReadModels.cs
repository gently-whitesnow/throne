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
    [property: Description("Attachment metadata. Bytes are NOT inlined; for each entry, call the tool named in 'recommended_tool' (read_intent_attachment_image for images, read_intent_attachment_text for text/log).")]
    IReadOnlyList<McpIntentAttachmentReadResult> Attachments);

public sealed record McpIntentListResult(
    [property: Description("Compact list of intents matching the filter.")] IReadOnlyList<McpIntentListItem> Items,
    [property: Description("Opaque cursor to fetch the next page; null when this is the last page.")] string? NextCursor);

public sealed record McpIntentListItem(
    [property: Description("Intent identifier.")] string Id,
    [property: Description("Current intent status.")] string Status,
    [property: Description("Current text version. Use as expected_version for write tools.")] int CurrentVersion,
    [property: Description("Tags currently attached to the intent.")] IReadOnlyList<McpTagRef> Tags,
    [property: Description("First non-empty line of Intent.text, trimmed to 200 characters.")] string Preview,
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
    [property: Description("Name of the MCP tool that returns the bytes for this attachment, or null if unsupported.")] string? RecommendedTool,
    [property: Description("True for image attachments that have been server-side downscaled to ≤1024 px JPEG q75.")] bool IsCompressedImage,
    [property: Description("Width in pixels of the stored image (post-compression). Null for non-image attachments.")] int? CompressedWidth,
    [property: Description("Height in pixels of the stored image (post-compression). Null for non-image attachments.")] int? CompressedHeight);
