using Throne.Api.Dreams;
using Throne.Api.InstructionPatches;
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
        IntentReordered reordered => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentReordered,
            new { intent_id = reordered.Intent.Id.Value, sort_key = reordered.Intent.SortKey }),
        IntentPinned pinned => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentPinned,
            new { intent_id = pinned.IntentId, context_tag_id = pinned.ContextTagId, pin_sort_key = pinned.PinSortKey }),
        IntentUnpinned unpinned => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentUnpinned,
            new { intent_id = unpinned.IntentId, context_tag_id = unpinned.ContextTagId }),
        IntentPinMoved pinMoved => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentPinMoved,
            new { intent_id = pinMoved.IntentId, context_tag_id = pinMoved.ContextTagId, pin_sort_key = pinMoved.PinSortKey }),
        IntentLinkAdded linkAdded => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentLinkAdded, IntentLinkDtoMapper.ToLinkDto(linkAdded.Link)),
        IntentLinkRemoved linkRemoved => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentLinkRemoved,
            new
            {
                id = linkRemoved.Link.Id,
                from_id = linkRemoved.Link.FromId.Value,
                to_id = linkRemoved.Link.ToId.Value,
                type = IntentLinkDtoMapper.ToContractLinkType(linkRemoved.Link.Type),
            }),
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
        InstructionPatchProposed proposed => new RealtimeEventEnvelope(
            RealtimeEventNames.InstructionPatchProposed, InstructionPatchDtoMapper.ToDto(proposed.Patch)),
        InstructionPatchApplied applied => new RealtimeEventEnvelope(
            RealtimeEventNames.InstructionPatchApplied, InstructionPatchDtoMapper.ToDto(applied.Patch)),
        InstructionPatchRejected rejected => new RealtimeEventEnvelope(
            RealtimeEventNames.InstructionPatchRejected, InstructionPatchDtoMapper.ToDto(rejected.Patch)),
        InstructionPatchSuperseded superseded => new RealtimeEventEnvelope(
            RealtimeEventNames.InstructionPatchSuperseded, InstructionPatchDtoMapper.ToDto(superseded.Patch)),
        DreamSessionRecorded dream => new RealtimeEventEnvelope(
            RealtimeEventNames.DreamSessionRecorded, DreamSessionDtoMapper.ToDto(dream.Session)),
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
