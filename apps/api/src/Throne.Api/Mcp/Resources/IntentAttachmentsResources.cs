using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Api.Mcp.Resources;

public sealed class IntentAttachmentsResources(
    IIntentRepository intents,
    IIntentAttachmentRepository attachments)
{
    private const string Scheme = "intent";
    private const string AttachmentsSegment = "attachments";

    public async ValueTask<ListResourcesResult> ListAsync(
        RequestContext<ListResourcesRequestParams> request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ownerIntents = await intents.ListAsync(statuses: null, cancellationToken);
        var resources = new List<Resource>();

        foreach (var intent in ownerIntents)
        {
            var count = await attachments.CountByIntentAsync(intent.Id, cancellationToken);
            if (count == 0)
            {
                continue;
            }

            resources.Add(BuildIntentResource(intent.Id));
        }

        return new ListResourcesResult { Resources = resources };
    }

    public async ValueTask<ReadResourceResult> ReadAsync(
        RequestContext<ReadResourceRequestParams> request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var uri = request.Params?.Uri
            ?? throw new ApiException(
                ErrorCodes.ValidationFailed,
                "resources/read requires a non-empty 'uri' parameter.",
                new Dictionary<string, object?>());

        if (!TryParseAttachmentsUri(uri, out var intentIdValue))
        {
            throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Resource '{uri}' is not a known Throne resource URI. " +
                $"Expected '{Scheme}://{{intent_id}}/{AttachmentsSegment}'.",
                new Dictionary<string, object?> { ["uri"] = uri });
        }

        var intentId = new IntentId(intentIdValue);

        var intent = await intents.GetByIdAsync(intentId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{intentIdValue}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = intentIdValue });

        var atts = await attachments.ListByIntentAsync(intent.Id, cancellationToken);
        var contents = new List<ResourceContents>(atts.Count);

        foreach (var attachment in atts)
        {
            var bytes = await ReadAttachmentBytesAsync(intent.Id, attachment.Id, cancellationToken);
            contents.Add(new BlobResourceContents
            {
                Uri = BuildAttachmentUri(intent.Id.Value, attachment.Id),
                MimeType = attachment.ContentType,
                Blob = Convert.ToBase64String(bytes),
            });
        }

        return new ReadResourceResult { Contents = contents };
    }

    private async Task<byte[]> ReadAttachmentBytesAsync(
        IntentId intentId,
        string attachmentId,
        CancellationToken ct)
    {
        var opened = await attachments.OpenContentAsync(intentId, attachmentId, ct)
            ?? throw new ApiException(
                ErrorCodes.IntentAttachmentNotFound,
                $"Attachment '{attachmentId}' not found on intent '{intentId.Value}'.",
                new Dictionary<string, object?>
                {
                    ["intent_id"] = intentId.Value,
                    ["attachment_id"] = attachmentId,
                });

        await using var stream = opened.Content;
        using var buffered = new MemoryStream();
        await stream.CopyToAsync(buffered, ct);
        return buffered.ToArray();
    }

    private static Resource BuildIntentResource(IntentId intentId)
    {
        var shortId = intentId.Value.Length >= 8 ? intentId.Value[..8] : intentId.Value;
        return new Resource
        {
            Uri = BuildIntentAttachmentsUri(intentId.Value),
            Name = $"Attachments of intent {shortId}",
            Description = "Binary attachments uploaded to this Throne intent. Read returns one BlobResourceContents per attachment.",
        };
    }

    private static string BuildIntentAttachmentsUri(string intentId) =>
        $"{Scheme}://{intentId}/{AttachmentsSegment}";

    private static string BuildAttachmentUri(string intentId, string attachmentId) =>
        $"{Scheme}://{intentId}/{AttachmentsSegment}/{attachmentId}";

    private static bool TryParseAttachmentsUri(string uri, out string intentId)
    {
        intentId = string.Empty;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (!string.Equals(parsed.Scheme, Scheme, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.IsNullOrEmpty(parsed.Host))
        {
            return false;
        }

        var path = parsed.AbsolutePath.TrimStart('/');
        if (!string.Equals(path, AttachmentsSegment, StringComparison.Ordinal))
        {
            return false;
        }

        intentId = parsed.Host;
        return true;
    }
}
