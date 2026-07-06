using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.TaskTrackers;

namespace Throne.Application.TaskTrackers.Attachments;

/// <summary>
/// Resolution helpers for <see cref="CardAttachmentService"/>: intent existence, attachment ownership, and
/// provider/connection resolution. Mirrors <c>RepositoryBindingResolver</c> so the service body stays a
/// thin workflow. A connection is resolved two ways — <see cref="ResolveConnectionAsync"/> throws the
/// attach-time failures (422/409), while <see cref="TryResolveConnectionAsync"/> returns null so refresh
/// can degrade without an error.
/// </summary>
public sealed class CardAttachmentResolver(
    IIntentRepository intents,
    IIntentCardAttachmentStore store,
    ITaskTrackerProviderRegistry registry,
    ITaskTrackerConnectionStore connections)
{
    public async Task<IntentId> EnsureIntentExistsAsync(string intentId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        var id = new IntentId(intentId);
        var intent = await intents.GetByIdAsync(id, ct)
            ?? throw CardAttachmentFailures.IntentNotFound(intentId);
        return intent.Id;
    }

    /// <summary>Load an attachment that belongs to the intent, or throw 404 (missing / foreign).</summary>
    public async Task<IntentCardAttachment> LoadAttachmentAsync(
        string intentId, string attachmentId, CancellationToken ct)
    {
        var attachment = await FindAttachmentAsync(intentId, attachmentId, ct)
            ?? throw CardAttachmentFailures.AttachmentNotFound(intentId, attachmentId);
        return attachment;
    }

    /// <summary>Load an attachment that belongs to the intent, or null — backs the idempotent detach.</summary>
    public async Task<IntentCardAttachment?> FindAttachmentAsync(
        string intentId, string attachmentId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(attachmentId);

        var attachment = await store.GetAsync(new CardAttachmentId(attachmentId), ct);
        return attachment is not null && attachment.IntentId.Value == intentId ? attachment : null;
    }

    /// <summary>Resolve a connectable provider + descriptor, throwing 422/409 when absent (attach path).</summary>
    public async Task<CardTrackerConnection> ResolveConnectionAsync(string tracker, CancellationToken ct)
    {
        var provider = registry.GetByName(tracker) as ITaskTrackerConnectionProvider
            ?? throw CardAttachmentFailures.TrackerUnsupported(tracker);
        var stored = await connections.GetAsync(tracker, ct)
            ?? throw CardAttachmentFailures.TrackerNotConnected(tracker);
        return new CardTrackerConnection(provider, new TaskTrackerConnectionDescriptor(stored.BaseUrl, stored.Token));
    }

    /// <summary>Resolve a connectable provider + descriptor, or null when unsupported/unconnected (refresh path).</summary>
    public async Task<CardTrackerConnection?> TryResolveConnectionAsync(string tracker, CancellationToken ct)
    {
        if (registry.GetByName(tracker) is not ITaskTrackerConnectionProvider provider)
        {
            return null;
        }

        var stored = await connections.GetAsync(tracker, ct);
        return stored is null
            ? null
            : new CardTrackerConnection(provider, new TaskTrackerConnectionDescriptor(stored.BaseUrl, stored.Token));
    }
}

/// <summary>A resolved connectable provider paired with its per-call connection descriptor.</summary>
public sealed record CardTrackerConnection(
    ITaskTrackerConnectionProvider Provider,
    TaskTrackerConnectionDescriptor Connection);
