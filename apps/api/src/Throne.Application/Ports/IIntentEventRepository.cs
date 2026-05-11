using Throne.Domain.Intents;
using Throne.Domain.Intents.Events;

namespace Throne.Application.Ports;

/// <summary>
/// Append-only feed of events on the Intent aggregate (ADR-0019). Stage-2 kinds:
/// <c>text_changed</c>, <c>link_added</c>, <c>link_removed</c>. Used by both the
/// HTTP timeline endpoint and the migration job that backfills history from
/// <c>text_versions</c>.
/// </summary>
public interface IIntentEventRepository
{
    /// <summary>
    /// Lists all events touching the supplied intent — that is, where
    /// <c>intent_id = id</c> OR <c>peer_intent_id = id</c>. Sorted by
    /// <c>created_at</c> ascending so the caller can render a chronological feed.
    /// </summary>
    Task<IReadOnlyList<IntentEvent>> ListByIntentAsync(IntentId intentId, CancellationToken ct);

    /// <summary>
    /// Lists only <c>text_changed</c> events for the intent — drop-in replacement
    /// for the legacy <c>list_intent_versions</c> read path. Sorted by version asc.
    /// </summary>
    Task<IReadOnlyList<IntentEvent>> ListTextChangesAsync(IntentId intentId, CancellationToken ct);

    /// <summary>
    /// Inserts a single event inside the current unit-of-work session.
    /// </summary>
    Task AppendAsync(IntentEvent evt, CancellationToken ct);
}
