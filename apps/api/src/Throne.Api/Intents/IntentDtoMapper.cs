using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Domain.Tags;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class IntentDtoMapper
{
    internal const int LinkPeerTextShortMaxLength = 140;

    public static IntentDetailDto ToDetailDto(
        Intent intent,
        IReadOnlyDictionary<string, Tag> tagsById,
        IReadOnlyList<IntentLinkViewDto>? links = null,
        IReadOnlyList<IntentPin>? pinnedIn = null) => new()
        {
            Id = intent.Id.Value,
            Status = IntentStatusDtoMapper.ToContractStatus(intent.State.Status),
            Current_version = intent.State.CurrentVersion,
            Tags = ToTagRefs(intent.TagIds, tagsById),
            Text = intent.State.Text,
            Sort_key = intent.State.SortKey,
            Created_at = intent.CreatedAt,
            Updated_at = intent.State.UpdatedAt,
            Links = links is null
                ? new System.Collections.ObjectModel.Collection<IntentLinkViewDto>()
                : new System.Collections.ObjectModel.Collection<IntentLinkViewDto>([.. links]),
            Pinned_in = ToPinnedContexts(pinnedIn),
        };

    public static IntentListItemDto ToListDto(
        Intent intent,
        IReadOnlyDictionary<string, Tag> tagsById,
        int textShortMaxLength,
        IReadOnlyList<IntentPin>? pinnedIn = null) => new()
        {
            Id = intent.Id.Value,
            Status = IntentStatusDtoMapper.ToContractStatus(intent.State.Status),
            Current_version = intent.State.CurrentVersion,
            Tags = ToTagRefs(intent.TagIds, tagsById),
            Text_short = TextShort(intent.State.Text, textShortMaxLength),
            Sort_key = intent.State.SortKey,
            Created_at = intent.CreatedAt,
            Updated_at = intent.State.UpdatedAt,
            Pinned_in = ToPinnedContexts(pinnedIn),
        };

    private static System.Collections.ObjectModel.Collection<PinnedContextDto> ToPinnedContexts(
        IReadOnlyList<IntentPin>? pins)
    {
        if (pins is null || pins.Count == 0)
        {
            return new System.Collections.ObjectModel.Collection<PinnedContextDto>();
        }
        var list = new List<PinnedContextDto>(pins.Count);
        foreach (var pin in pins)
        {
            list.Add(new PinnedContextDto
            {
                Context_tag_id = pin.ContextTagId.Value,
                Pin_sort_key = pin.PinSortKey,
            });
        }
        return new System.Collections.ObjectModel.Collection<PinnedContextDto>(list);
    }

    public static IntentLinkPeerDto ToPeerDto(Intent peer, IReadOnlyDictionary<string, Tag> tagsById) => new()
    {
        Id = peer.Id.Value,
        Status = IntentStatusDtoMapper.ToContractStatus(peer.State.Status),
        Current_version = peer.State.CurrentVersion,
        Sort_key = peer.State.SortKey,
        Text_short = TextShort(peer.State.Text, LinkPeerTextShortMaxLength),
        Tags = ToTagRefs(peer.TagIds, tagsById),
    };

    public static IntentAttachmentDto ToAttachmentDto(IntentAttachment attachment) => new()
    {
        Id = attachment.Id,
        Intent_id = attachment.IntentId,
        File_name = attachment.FileName,
        Content_type = attachment.ContentType,
        Size_bytes = attachment.SizeBytes,
        Created_at = attachment.CreatedAt,
    };

    private static List<TagRefDto> ToTagRefs(IReadOnlyList<TagId> tagIds, IReadOnlyDictionary<string, Tag> tagsById)
    {
        var refs = new List<TagRefDto>(tagIds.Count);
        foreach (var id in tagIds)
        {
            if (!tagsById.TryGetValue(id.Value, out var tag))
            {
                continue;
            }

            refs.Add(new TagRefDto { Id = tag.Id.Value, Name = tag.Name });
        }
        return refs;
    }

    private static string TextShort(string text, int max) =>
        text.Length <= max ? text : text[..max];
}
