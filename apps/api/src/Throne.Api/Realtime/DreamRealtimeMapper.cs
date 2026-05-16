using Throne.Api.Dreams;
using Throne.Application.Events;
using Throne.Realtime.Contracts;
using Throne.Realtime.Contracts.Generated;

namespace Throne.Api.Realtime;

internal static class DreamRealtimeMapper
{
    public static RealtimeEventEnvelope? TryMap(IDomainEvent evt) => evt switch
    {
        DreamSessionRecorded dream => new RealtimeEventEnvelope(
            RealtimeEventNames.DreamSessionRecorded, DreamSessionDtoMapper.ToDto(dream.Session)),
        _ => null,
    };
}
