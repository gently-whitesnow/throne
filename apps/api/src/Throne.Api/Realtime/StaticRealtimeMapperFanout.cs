using Throne.Application.Events;
using Throne.Realtime.Contracts;

namespace Throne.Api.Realtime;

internal static class StaticRealtimeMapperFanout
{
    public static RealtimeEventEnvelope? TryMap(IDomainEvent evt) =>
        IntentPinRealtimeMapper.TryMap(evt)
            ?? IntentLinkRealtimeMapper.TryMap(evt)
            ?? IntentAttachmentRealtimeMapper.TryMap(evt)
            ?? IntentRepositoryRealtimeMapper.TryMap(evt)
            ?? TagRealtimeMapper.TryMap(evt)
            ?? InstructionPatchRealtimeMapper.TryMap(evt)
            ?? DreamRealtimeMapper.TryMap(evt);
}
