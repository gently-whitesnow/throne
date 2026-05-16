using Throne.Domain.Intents;
using Throne.Domain.Tags;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class IntentLinkPeerDtoMapper
{
    internal const int TextShortMaxLength = 140;

    public static IntentLinkPeerDto ToPeerDto(Intent peer, IReadOnlyDictionary<string, Tag> tagsById) => new()
    {
        Id = peer.Id.Value,
        Status = IntentStatusDtoMapper.ToContractStatus(peer.State.Status),
        Current_version = peer.State.CurrentVersion,
        Sort_key = peer.State.SortKey,
        Text_short = IntentTextSnippet.Cut(peer.State.Text, TextShortMaxLength),
        Tags = IntentTagDtoMapper.ToTagRefs(peer.TagIds, tagsById),
    };
}
