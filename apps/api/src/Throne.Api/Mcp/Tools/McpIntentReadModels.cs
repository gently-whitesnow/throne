using System.ComponentModel;

namespace Throne.Api.Mcp.Tools;

public sealed record McpIntentReadResult(
    [property: Description("Intent identifier.")] string Id,
    [property: Description("Full canonical Intent.text.")] string Text,
    [property: Description("Current text version.")] int CurrentVersion,
    [property: Description("Normalized tags.")] IReadOnlyList<string> Tags,
    [property: Description("Creation timestamp.")] DateTimeOffset CreatedAt,
    [property: Description("Last update timestamp.")] DateTimeOffset UpdatedAt,
    [property: Description("Attachment metadata. Image bytes are returned as MCP image content blocks, not inline JSON.")]
    IReadOnlyList<McpIntentAttachmentReadResult> Attachments,
    [property: Description("Number of image content blocks included in the MCP tool response.")]
    int ImageContentBlocksReturned);

public sealed record McpIntentAttachmentReadResult(
    [property: Description("Attachment identifier.")] string Id,
    [property: Description("Owning intent identifier.")] string IntentId,
    [property: Description("Original file name.")] string FileName,
    [property: Description("Declared MIME type.")] string ContentType,
    [property: Description("Stored size in bytes.")] long SizeBytes,
    [property: Description("Upload timestamp.")] DateTimeOffset CreatedAt,
    [property: Description("True when this image was returned as an MCP image content block.")]
    bool ImageContentReturned);
