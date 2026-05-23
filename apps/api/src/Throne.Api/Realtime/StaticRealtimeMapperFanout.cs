using Throne.Application.Events;
using Throne.Realtime.Contracts;

namespace Throne.Api.Realtime;

/// <summary>
/// Ordered chain of stateless per-aggregate mappers. Lives apart from
/// <see cref="RealtimeDomainEventHandler"/> so the per-class CA1502 cyclomatic budget
/// (which sums all members) accommodates further mapper additions without touching
/// the dispatcher.
/// </summary>
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
