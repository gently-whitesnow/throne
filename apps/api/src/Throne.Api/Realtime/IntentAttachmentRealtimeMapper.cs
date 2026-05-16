using Throne.Api.Intents;
using Throne.Application.Events;
using Throne.Realtime.Contracts;
using Throne.Realtime.Contracts.Generated;

namespace Throne.Api.Realtime;

internal static class IntentAttachmentRealtimeMapper
{
    public static RealtimeEventEnvelope? TryMap(IDomainEvent evt) => evt switch
    {
        IntentAttachmentAdded added => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentAttachmentAdded, IntentDtoMapper.ToAttachmentDto(added.Attachment)),
        IntentAttachmentDeleted deleted => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentAttachmentDeleted,
            new { intent_id = deleted.IntentId, attachment_id = deleted.AttachmentId }),
        _ => null,
    };
}
