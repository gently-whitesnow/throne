using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Events;

namespace Throne.Application.Intents.Events;

public sealed record ListIntentEventsQuery(string IntentId);

/// <summary>
/// Returns the unified append-only event feed for an intent (ADR-0019). The repository
/// already filters by `intent_id = X OR peer_intent_id = X` so link events on either
/// endpoint surface in this list, sorted chronologically.
/// </summary>
public sealed class ListIntentEventsHandler(
    IIntentRepository intents,
    IIntentEventRepository intentEvents)
{
    public async Task<IReadOnlyList<IntentEvent>> HandleAsync(ListIntentEventsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.IntentId);

        var id = new IntentId(query.IntentId);
        _ = await intents.GetByIdAsync(id, ct)
            ?? throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{query.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = query.IntentId });

        return await intentEvents.ListByIntentAsync(id, ct);
    }
}
