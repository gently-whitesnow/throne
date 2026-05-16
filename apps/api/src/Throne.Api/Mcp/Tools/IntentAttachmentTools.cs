using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Throne.Application.Errors;
using Throne.Application.Intents.Attachments;

namespace Throne.Api.Mcp.Tools;

[McpServerToolType]
public sealed class IntentAttachmentTools(IntentAttachmentLoader loader)
{
    private const long MaxImageBytes = 5L * 1024 * 1024;

    [McpServerTool(Name = "read_intent_attachment_image", ReadOnly = true, UseStructuredContent = false)]
    [Description("Return an image attachment as a native image content block (vision tokens, not text). Use this when the get_intent attachment entry has kind='image'. Server-side compressed JPEG ≤1024 px.")]
    public async Task<CallToolResult> ReadIntentAttachmentImage(
        [Description("Intent id owning the attachment.")] string intent_id,
        [Description("Attachment id from get_intent.attachments[].id.")] string attachment_id,
        CancellationToken cancellationToken = default)
    {
        var (att, bytes) = await loader.LoadAsync(intent_id, attachment_id, AttachmentKind.Image, cancellationToken);

        if (bytes.LongLength > MaxImageBytes)
        {
            throw new ApiException(
                ErrorCodes.IntentAttachmentTooLarge,
                $"Attachment '{attachment_id}' is {bytes.LongLength.ToString(System.Globalization.CultureInfo.InvariantCulture)} bytes after server compression, which exceeds the 5 MB vision-input limit. Re-upload a smaller image.",
                new Dictionary<string, object?>
                {
                    ["intent_id"] = intent_id,
                    ["attachment_id"] = attachment_id,
                    ["size_bytes"] = bytes.LongLength,
                });
        }

        return new CallToolResult
        {
            Content =
            [
                new ImageContentBlock
                {
                    Data = bytes,
                    MimeType = att.ContentType,
                },
            ],
            IsError = false,
        };
    }

    [McpServerTool(Name = "read_intent_attachment_text", ReadOnly = true, UseStructuredContent = true)]
    [Description("Return a UTF-8 slice of a text attachment (logs, JSON, markdown, plain text). Use when the get_intent entry has kind='text'. offset is in bytes; max_chars caps decoded characters (default 50000, absolute max 200000). When truncated=true, call again with offset = returned_bytes_end to continue.")]
    public async Task<IntentAttachmentTextSlice> ReadIntentAttachmentText(
        [Description("Intent id owning the attachment.")] string intent_id,
        [Description("Attachment id from get_intent.attachments[].id.")] string attachment_id,
        [Description("Byte offset to start reading at. Defaults to 0.")] int? offset = null,
        [Description("Maximum number of characters to return after UTF-8 decode. Defaults to 50000, absolute max 200000.")] int? max_chars = null,
        CancellationToken cancellationToken = default)
    {
        var (att, bytes) = await loader.LoadAsync(intent_id, attachment_id, AttachmentKind.Text, cancellationToken);
        return IntentAttachmentTextSlicer.Slice(att.ContentType, bytes, offset, max_chars);
    }
}

public sealed record IntentAttachmentTextSlice(
    [property: Description("MIME type as stored.")] string ContentType,
    [property: Description("Total size of the attachment in bytes.")] long TotalSizeBytes,
    [property: Description("Inclusive byte offset of the first returned byte (after UTF-8-safe alignment when offset > 0).")] int ReturnedBytesStart,
    [property: Description("Exclusive byte offset just past the last returned byte. Pass as 'offset' to read the next chunk.")] int ReturnedBytesEnd,
    [property: Description("True when more bytes remain or the max_chars cap was hit. Continue reading with offset = returned_bytes_end.")] bool Truncated,
    [property: Description("Decoded UTF-8 text slice. Length is at most max_chars characters.")] string Text);
