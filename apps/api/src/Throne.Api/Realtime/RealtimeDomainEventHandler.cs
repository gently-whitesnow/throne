using Throne.Api.Dream;
using Throne.Api.Intents;
using Throne.Api.Tags;
using Throne.Application.Events;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Tags;
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
internal sealed class RealtimeDomainEventHandler(
    IRealtimeEventBroker broker,
    ITagRepository tags) : IDomainEventHandler
{
    public async Task HandleAsync(IDomainEvent evt, CancellationToken ct)
    {
        var envelope = await ToEnvelopeAsync(evt, ct);
        if (envelope is null)
        {
            return;
        }

        await broker.PublishAsync(envelope, ct);
    }

    private async Task<RealtimeEventEnvelope?> ToEnvelopeAsync(IDomainEvent evt, CancellationToken ct) => evt switch
    {
        IntentCreated created => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentCreated, IntentDtoMapper.ToDetailDto(created.Intent, await ResolveTagMapAsync(created.Intent, ct))),
        IntentDeleted deleted => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentDeleted, new { intent_id = deleted.IntentId }),
        IntentTextChanged text => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentTextChanged, IntentDtoMapper.ToDetailDto(text.Intent, await ResolveTagMapAsync(text.Intent, ct))),
        IntentStatusChanged status => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentStatusChanged, IntentDtoMapper.ToDetailDto(status.Intent, await ResolveTagMapAsync(status.Intent, ct))),
        IntentTagsChanged tagsChanged => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentTagsChanged, IntentDtoMapper.ToDetailDto(tagsChanged.Intent, await ResolveTagMapAsync(tagsChanged.Intent, ct))),
        IntentQaAdded qa => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentQaAdded, IntentDtoMapper.ToQaDto(qa.Qa)),
        IntentReviewAdded review => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentReviewAdded, IntentDtoMapper.ToReviewDto(review.Review)),
        IntentAttachmentAdded added => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentAttachmentAdded, IntentDtoMapper.ToAttachmentDto(added.Attachment)),
        IntentAttachmentDeleted deleted => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentAttachmentDeleted,
            new { intent_id = deleted.IntentId, attachment_id = deleted.AttachmentId }),
        TagCreated created => new RealtimeEventEnvelope(
            RealtimeEventNames.TagCreated, TagDtoMapper.ToDto(created.Tag)),
        TagUpdated updated => new RealtimeEventEnvelope(
            RealtimeEventNames.TagUpdated, TagDtoMapper.ToDto(updated.Tag)),
        TagDeleted deleted => new RealtimeEventEnvelope(
            RealtimeEventNames.TagDeleted, new { tag_id = deleted.TagId }),
        DreamRunCreated created => new RealtimeEventEnvelope(
            RealtimeEventNames.DreamRunCreated, DreamDtoMapper.ToRunDto(created.Run)),
        DreamProposalCreated created => new RealtimeEventEnvelope(
            RealtimeEventNames.DreamProposalCreated, DreamDtoMapper.ToRunDto(created.Run)),
        DreamProposalApplied applied => new RealtimeEventEnvelope(
            RealtimeEventNames.DreamProposalApplied, DreamDtoMapper.ToRunDto(applied.Run)),
        DreamProposalSkipped skipped => new RealtimeEventEnvelope(
            RealtimeEventNames.DreamProposalSkipped, DreamDtoMapper.ToRunDto(skipped.Run)),
        DreamRunClosed closed => new RealtimeEventEnvelope(
            RealtimeEventNames.DreamRunClosed, DreamDtoMapper.ToRunDto(closed.Run)),
        DreamFuelChanged fuel => new RealtimeEventEnvelope(
            RealtimeEventNames.DreamFuelChanged,
            new { available_tokens = fuel.AvailableTokens, status = fuel.Status }),
        _ => null,
    };

    private async Task<IReadOnlyDictionary<string, Tag>> ResolveTagMapAsync(Intent intent, CancellationToken ct)
    {
        if (intent.TagIds.Count == 0)
        {
            return new Dictionary<string, Tag>(StringComparer.Ordinal);
        }

        var ids = intent.TagIds.Select(t => t.Value).ToHashSet(StringComparer.Ordinal);
        var all = await tags.ListAllAsync(ct);
        var map = new Dictionary<string, Tag>(StringComparer.Ordinal);
        foreach (var t in all)
        {
            if (ids.Contains(t.Id.Value))
            {
                map[t.Id.Value] = t;
            }
        }
        return map;
    }
}
