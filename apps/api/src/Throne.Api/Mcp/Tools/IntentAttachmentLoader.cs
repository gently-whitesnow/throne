using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.Intents.Attachments;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Api.Mcp.Tools;

public sealed class IntentAttachmentLoader(
    IIntentRepository intents,
    IIntentAttachmentRepository attachments)
{
    public async Task<(IntentAttachment Attachment, byte[] Bytes)> LoadAsync(
        string intentId,
        string attachmentId,
        AttachmentKind expected,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(intentId))
        {
            throw McpToolErrors.ValidationFailed("intent_id must be non-empty.");
        }
        if (string.IsNullOrWhiteSpace(attachmentId))
        {
            throw McpToolErrors.ValidationFailed("attachment_id must be non-empty.");
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

        EnsureKindMatches(opened.Attachment, intentId, attachmentId, expected);

        await using var stream = opened.Content;
        using var buffered = new MemoryStream();
        await stream.CopyToAsync(buffered, ct);
        return (opened.Attachment, buffered.ToArray());
    }

    private static void EnsureKindMatches(
        IntentAttachment attachment,
        string intentId,
        string attachmentId,
        AttachmentKind expected)
    {
        var actualKind = AttachmentKindResolver.Resolve(attachment.ContentType);
        if (actualKind == expected)
        {
            return;
        }

        var hint = AttachmentKindResolver.RecommendedTool(actualKind);
        var message = hint is null
            ? $"Attachment '{attachmentId}' has unsupported content type '{attachment.ContentType}'."
            : $"Attachment '{attachmentId}' is '{attachment.ContentType}' (kind={AttachmentKindResolver.KindWireName(actualKind)}); use {hint} instead.";

        throw new ApiException(
            ErrorCodes.ValidationFailed,
            message,
            new Dictionary<string, object?>
            {
                ["intent_id"] = intentId,
                ["attachment_id"] = attachmentId,
                ["content_type"] = attachment.ContentType,
                ["kind"] = AttachmentKindResolver.KindWireName(actualKind),
            });
    }
}

internal static class McpToolErrors
{
    public static ApiException ValidationFailed(string message) =>
        new(ErrorCodes.ValidationFailed, message, new Dictionary<string, object?>());
}
