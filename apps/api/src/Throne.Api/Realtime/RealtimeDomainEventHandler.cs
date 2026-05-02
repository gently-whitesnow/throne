using Throne.Api.Intents;
using Throne.Application.Events;
using Throne.Realtime.Contracts;
using Throne.Realtime.Contracts.Generated;

namespace Throne.Api.Realtime;

/// <summary>
/// The single sink that turns domain events into realtime SSE envelopes.
///
/// Adding a new realtime event:
///   1. add the entry to specs/contracts/realtime/events.yaml,
///   2. add a corresponding <see cref="IDomainEvent"/> record under
///      Throne.Application.Events,
///   3. add a case below mapping the domain event to the right
///      <see cref="RealtimeEventNames"/> constant + DTO,
///   4. raise the event from the appropriate repository outcome,
///   5. subscribe via useRealtimeEvent('&lt;name&gt;') on the frontend.
///
/// The realtime quality gate (scripts/quality/realtime-verify-coverage.sh)
/// fails until all five are present.
/// </summary>
internal sealed class RealtimeDomainEventHandler(IRealtimeEventBroker broker) : IDomainEventHandler
{
    private static RealtimeEventEnvelope? ToEnvelope(IDomainEvent evt) => evt switch
    {
        IntentCreated created => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentCreated, IntentDtoMapper.ToDetailDto(created.Intent)),
        IntentDeleted deleted => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentDeleted, new { intent_id = deleted.IntentId }),
        IntentTextChanged text => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentTextChanged, IntentDtoMapper.ToDetailDto(text.Intent)),
        IntentStatusChanged status => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentStatusChanged, IntentDtoMapper.ToDetailDto(status.Intent)),
        IntentQaAdded qa => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentQaAdded, IntentDtoMapper.ToQaDto(qa.Qa)),
        IntentReviewAdded review => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentReviewAdded, IntentDtoMapper.ToReviewDto(review.Review)),
        IntentAttachmentAdded added => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentAttachmentAdded, IntentDtoMapper.ToAttachmentDto(added.Attachment)),
        IntentAttachmentDeleted deleted => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentAttachmentDeleted,
            new { intent_id = deleted.IntentId, attachment_id = deleted.AttachmentId }),
        _ => null,
    };

    public async Task HandleAsync(IDomainEvent evt, CancellationToken ct)
    {
        var envelope = ToEnvelope(evt);
        if (envelope is null)
        {
            return;
        }

        await broker.PublishAsync(envelope, ct).ConfigureAwait(false);
    }
}
