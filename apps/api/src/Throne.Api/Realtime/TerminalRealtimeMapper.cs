using Throne.Application.Events;
using Throne.Realtime.Contracts;
using Throne.Realtime.Contracts.Generated;

namespace Throne.Api.Realtime;

internal static class TerminalRealtimeMapper
{
    public static RealtimeEventEnvelope? TryMap(IDomainEvent evt) => evt switch
    {
        TerminalSessionStarted started => new RealtimeEventEnvelope(
            RealtimeEventNames.TerminalSessionStarted,
            new { intent_id = started.IntentId }),
        TerminalSessionStopped stopped => new RealtimeEventEnvelope(
            RealtimeEventNames.TerminalSessionStopped,
            new { intent_id = stopped.IntentId }),
        _ => null,
    };
}
