using Throne.Application.Events;
using Throne.Realtime.Contracts;
using Throne.Realtime.Contracts.Generated;

namespace Throne.Api.Realtime;

internal static class IntentPinRealtimeMapper
{
    public static RealtimeEventEnvelope? TryMap(IDomainEvent evt) => evt switch
    {
        IntentPinned pinned => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentPinned,
            new { intent_id = pinned.IntentId, context_tag_id = pinned.ContextTagId, pin_sort_key = pinned.PinSortKey }),
        IntentUnpinned unpinned => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentUnpinned,
            new { intent_id = unpinned.IntentId, context_tag_id = unpinned.ContextTagId }),
        IntentPinMoved pinMoved => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentPinMoved,
            new { intent_id = pinMoved.IntentId, context_tag_id = pinMoved.ContextTagId, pin_sort_key = pinMoved.PinSortKey }),
        _ => null,
    };
}
