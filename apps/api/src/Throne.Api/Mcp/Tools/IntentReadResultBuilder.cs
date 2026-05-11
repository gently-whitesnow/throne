using Throne.Application.Intents;
using Throne.Application.Intents.Attachments;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Api.Mcp.Tools;

internal static class IntentReadResultBuilder
{
    public static McpIntentReadResult Build(
        Intent intent,
        IReadOnlyList<IntentAttachment> attachments,
        IReadOnlyList<IntentLinkView> links,
        Dictionary<string, McpTagRef> tagsById) => new(
        intent.Id.Value,
        intent.Text,
        intent.Status,
        intent.CurrentVersion,
        OwnTags(intent, tagsById),
        intent.SortKey,
        intent.CreatedAt,
        intent.UpdatedAt,
        attachments.Select(ToAttachmentResult).ToArray(),
        links.Select(v => IntentLinkTools.ToMcpLinkRead(v, tagsById)).ToList());

    private static List<McpTagRef> OwnTags(Intent intent, Dictionary<string, McpTagRef> tagsById) =>
        intent.TagIds
            .Select(id => tagsById.TryGetValue(id.Value, out var t) ? t : null)
            .Where(t => t is not null)
            .Select(t => t!)
            .ToList();

    private static McpIntentAttachmentReadResult ToAttachmentResult(IntentAttachment a)
    {
        var kind = AttachmentKindResolver.Resolve(a.ContentType);
        return new McpIntentAttachmentReadResult(
            a.Id,
            a.IntentId,
            a.FileName,
            a.ContentType,
            a.SizeBytes,
            a.CreatedAt,
            AttachmentKindResolver.KindWireName(kind),
            AttachmentKindResolver.RecommendedTool(kind),
            a.IsCompressed,
            a.CompressedWidth,
            a.CompressedHeight);
    }
}
