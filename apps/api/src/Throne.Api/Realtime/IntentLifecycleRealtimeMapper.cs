using Throne.Api.Intents;
using Throne.Application.Events;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Tags;
using Throne.Realtime.Contracts;
using Throne.Realtime.Contracts.Generated;

namespace Throne.Api.Realtime;

internal sealed class IntentLifecycleRealtimeMapper(ITagRepository tags)
{
    public async Task<RealtimeEventEnvelope?> TryMapAsync(IDomainEvent evt, CancellationToken ct) => evt switch
    {
        IntentCreated created => await MakeIntentDetailEnvelopeAsync(
            RealtimeEventNames.IntentCreated, created.Intent, ct),
        IntentDeleted deleted => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentDeleted, new { intent_id = deleted.IntentId }),
        IntentTextChanged text => await MakeIntentDetailEnvelopeAsync(
            RealtimeEventNames.IntentTextChanged, text.Intent, ct),
        IntentStatusChanged status => await MakeIntentDetailEnvelopeAsync(
            RealtimeEventNames.IntentStatusChanged, status.Intent, ct),
        IntentTagsChanged tagsChanged => await MakeIntentDetailEnvelopeAsync(
            RealtimeEventNames.IntentTagsChanged, tagsChanged.Intent, ct),
        IntentReordered reordered => new RealtimeEventEnvelope(
            RealtimeEventNames.IntentReordered,
            new { intent_id = reordered.Intent.Id.Value, sort_key = reordered.Intent.State.SortKey }),
        _ => null,
    };

    private async Task<RealtimeEventEnvelope> MakeIntentDetailEnvelopeAsync(
        string eventName, Intent intent, CancellationToken ct)
    {
        var tagMap = await ResolveTagMapAsync(intent, ct);
        return new RealtimeEventEnvelope(eventName, IntentDtoMapper.ToDetailDto(intent, tagMap));
    }

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
