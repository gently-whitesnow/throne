// MCP wire-format requires snake_case parameter names; tools are an API boundary.
#pragma warning disable CA1707
using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.Intents.Attachments;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Api.Mcp.Tools;

[McpServerToolType]
public sealed class IntentAttachmentTools(
    IIntentRepository intents,
    IIntentAttachmentRepository attachments)
{
    private const long MaxImageBytes = 5L * 1024 * 1024;
    private const int DefaultMaxChars = 50_000;
    private const int AbsoluteMaxChars = 200_000;

    [McpServerTool(Name = "read_intent_attachment_image", ReadOnly = true, UseStructuredContent = false)]
    [Description("Return an image attachment as a native image content block (vision tokens, not text). Use this when the get_intent attachment entry has kind='image'. Server-side compressed JPEG ≤1024 px.")]
    public async Task<CallToolResult> ReadIntentAttachmentImage(
        [Description("Intent id owning the attachment.")] string intent_id,
        [Description("Attachment id from get_intent.attachments[].id.")] string attachment_id,
        CancellationToken cancellationToken = default)
    {
        var (att, bytes) = await LoadAsync(intent_id, attachment_id, AttachmentKind.Image, cancellationToken);

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
                    Data = Convert.ToBase64String(bytes),
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
        var startOffset = offset.GetValueOrDefault(0);
        if (startOffset < 0)
        {
            throw ValidationFailed("offset must be non-negative.");
        }

        var charLimit = max_chars.GetValueOrDefault(DefaultMaxChars);
        if (charLimit <= 0)
        {
            throw ValidationFailed("max_chars must be positive.");
        }
        if (charLimit > AbsoluteMaxChars)
        {
            throw ValidationFailed($"max_chars must be ≤ {AbsoluteMaxChars}.");
        }

        var (att, bytes) = await LoadAsync(intent_id, attachment_id, AttachmentKind.Text, cancellationToken);
        var totalBytes = bytes.Length;

        if (startOffset > totalBytes)
        {
            startOffset = totalBytes;
        }

        // Cap window at 4*charLimit bytes (UTF-8 max 4 bytes per char).
        var maxWindowBytes = checked((long)charLimit * 4);
        var windowEndExclusive = (int)Math.Min(totalBytes, startOffset + maxWindowBytes);
        var window = bytes.AsSpan(startOffset, windowEndExclusive - startOffset);

        // Drop leading UTF-8 continuation bytes if we're not at the start of a rune.
        var skipLeading = 0;
        if (startOffset > 0)
        {
            while (skipLeading < window.Length && (window[skipLeading] & 0xC0) == 0x80)
            {
                skipLeading++;
            }
        }

        var trimmed = window[skipLeading..];
        var decoded = Encoding.UTF8.GetString(trimmed);

        var truncatedByCharLimit = decoded.Length > charLimit;
        if (truncatedByCharLimit)
        {
            decoded = decoded[..charLimit];
        }

        var returnedByteLength = Encoding.UTF8.GetByteCount(decoded);
        var returnedBytesStart = startOffset + skipLeading;
        var returnedBytesEnd = returnedBytesStart + returnedByteLength;
        var truncated = truncatedByCharLimit || returnedBytesEnd < totalBytes;

        return new IntentAttachmentTextSlice(
            att.ContentType,
            totalBytes,
            returnedBytesStart,
            returnedBytesEnd,
            truncated,
            decoded);
    }

    private async Task<(IntentAttachment Attachment, byte[] Bytes)> LoadAsync(
        string intentId,
        string attachmentId,
        AttachmentKind expected,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(intentId))
        {
            throw ValidationFailed("intent_id must be non-empty.");
        }
        if (string.IsNullOrWhiteSpace(attachmentId))
        {
            throw ValidationFailed("attachment_id must be non-empty.");
        }

        var typedIntentId = new IntentId(intentId);
        if (await intents.GetByIdAsync(typedIntentId, ct) is null)
        {
            throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{intentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = intentId });
        }

        var opened = await attachments.OpenContentAsync(typedIntentId, attachmentId, ct)
            ?? throw new ApiException(
                ErrorCodes.IntentAttachmentNotFound,
                $"Attachment '{attachmentId}' not found on intent '{intentId}'.",
                new Dictionary<string, object?>
                {
                    ["intent_id"] = intentId,
                    ["attachment_id"] = attachmentId,
                });

        var actualKind = AttachmentKindResolver.Resolve(opened.Attachment.ContentType);
        if (actualKind != expected)
        {
            var hint = AttachmentKindResolver.RecommendedTool(actualKind);
            var message = hint is null
                ? $"Attachment '{attachmentId}' has unsupported content type '{opened.Attachment.ContentType}'."
                : $"Attachment '{attachmentId}' is '{opened.Attachment.ContentType}' (kind={AttachmentKindResolver.KindWireName(actualKind)}); use {hint} instead.";

            throw new ApiException(
                ErrorCodes.ValidationFailed,
                message,
                new Dictionary<string, object?>
                {
                    ["intent_id"] = intentId,
                    ["attachment_id"] = attachmentId,
                    ["content_type"] = opened.Attachment.ContentType,
                    ["kind"] = AttachmentKindResolver.KindWireName(actualKind),
                });
        }

        await using var stream = opened.Content;
        using var buffered = new MemoryStream();
        await stream.CopyToAsync(buffered, ct);
        return (opened.Attachment, buffered.ToArray());
    }

    private static ApiException ValidationFailed(string message) =>
        new(ErrorCodes.ValidationFailed, message, new Dictionary<string, object?>());
}

public sealed record IntentAttachmentTextSlice(
    [property: Description("MIME type as stored.")] string ContentType,
    [property: Description("Total size of the attachment in bytes.")] long TotalSizeBytes,
    [property: Description("Inclusive byte offset of the first returned byte (after UTF-8-safe alignment when offset > 0).")] int ReturnedBytesStart,
    [property: Description("Exclusive byte offset just past the last returned byte. Pass as 'offset' to read the next chunk.")] int ReturnedBytesEnd,
    [property: Description("True when more bytes remain or the max_chars cap was hit. Continue reading with offset = returned_bytes_end.")] bool Truncated,
    [property: Description("Decoded UTF-8 text slice. Length is at most max_chars characters.")] string Text);
