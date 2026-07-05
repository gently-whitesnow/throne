using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Tags;

namespace Throne.Application.Terminals;

/// <summary>
/// Resolves the per-delivery workspace-map context — tag names, link micro-facts and the intent's
/// attachments — in one call, off the Run pre-flight critical path (delivery is detached). Bundled so
/// <see cref="RunPreflightPromptDelivery"/> takes a single collaborator instead of three separate
/// readers: the three are the same «resolve by intent id at delivery» concern.
/// </summary>
public sealed class TerminalDeliveryMapContextReader(
    RunPreflightTagNames tagNames,
    IntentLinkPromptContextReader linkContext,
    IIntentAttachmentRepository attachments)
{
    public async Task<TerminalDeliveryMapContext> ReadAsync(
        string intentId, IReadOnlyList<TagId> tagIds, CancellationToken ct)
    {
        var id = new IntentId(intentId);
        var tags = await tagNames.ResolveAsync(tagIds, ct);
        var links = await linkContext.BuildAsync(id, ct);
        var attachmentList = await attachments.ListByIntentAsync(id, ct);
        return new TerminalDeliveryMapContext(tags, links, attachmentList);
    }
}

public sealed record TerminalDeliveryMapContext(
    IReadOnlyList<string> Tags,
    IReadOnlyList<IntentLinkPromptContext> Links,
    IReadOnlyList<IntentAttachment> Attachments);
