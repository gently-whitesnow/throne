using Throne.Api.Intents;
using Throne.Application.Events;
using Throne.Realtime.Contracts;
using Throne.Realtime.Contracts.Generated;

namespace Throne.Api.Realtime;

internal static class IntentLinkRealtimeMapper
{
    public static RealtimeEventEnvelope? TryMap(IDomainEvent evt) => evt switch
    {
        IntentLinkAdded linkAdded => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentLinkAdded, IntentLinkDtoMapper.ToLinkDto(linkAdded.Link)),
        IntentLinkRemoved linkRemoved => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentLinkRemoved,
            new
            {
                id = linkRemoved.Link.Id,
                from_id = linkRemoved.Link.FromId.Value,
                to_id = linkRemoved.Link.ToId.Value,
                blocking = linkRemoved.Link.Blocking,
            }),
        _ => null,
    };
}
