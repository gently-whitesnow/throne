using Throne.Api.Tags;
using Throne.Application.Events;
using Throne.Realtime.Contracts;
using Throne.Realtime.Contracts.Generated;

namespace Throne.Api.Realtime;

internal static class TagRealtimeMapper
{
    public static RealtimeEventEnvelope? TryMap(IDomainEvent evt) => evt switch
    {
        TagCreated created => new RealtimeEventEnvelope(
            RealtimeEventNames.TagCreated, TagDtoMapper.ToDto(created.Tag)),
        TagUpdated updated => new RealtimeEventEnvelope(
            RealtimeEventNames.TagUpdated, TagDtoMapper.ToDto(updated.Tag)),
        TagDeleted deleted => new RealtimeEventEnvelope(
            RealtimeEventNames.TagDeleted, new { tag_id = deleted.TagId }),
        _ => null,
    };
}
