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
        IReadOnlyList<IntentLinkViewDto>? links = null) => new()
        {
            Id = intent.Id.Value,
            Status = IntentStatusDtoMapper.ToContractStatus(intent.Status),
            Current_version = intent.CurrentVersion,
            Tags = ToTagRefs(intent.TagIds, tagsById),
            Text = intent.Text,
            Sort_key = intent.SortKey,
            Created_at = intent.CreatedAt,
            Updated_at = intent.UpdatedAt,
            Links = links is null
                ? new System.Collections.ObjectModel.Collection<IntentLinkViewDto>()
                : new System.Collections.ObjectModel.Collection<IntentLinkViewDto>([.. links]),
        };

    public static IntentListItemDto ToListDto(
        Intent intent,
        IReadOnlyDictionary<string, Tag> tagsById,
        int textShortMaxLength) => new()
        {
            Id = intent.Id.Value,
            Status = IntentStatusDtoMapper.ToContractStatus(intent.Status),
            Current_version = intent.CurrentVersion,
            Tags = ToTagRefs(intent.TagIds, tagsById),
            Text_short = TextShort(intent.Text, textShortMaxLength),
            Sort_key = intent.SortKey,
            Created_at = intent.CreatedAt,
            Updated_at = intent.UpdatedAt,
        };

    public static IntentLinkPeerDto ToPeerDto(Intent peer, IReadOnlyDictionary<string, Tag> tagsById) => new()
    {
        Id = peer.Id.Value,
        Status = IntentStatusDtoMapper.ToContractStatus(peer.Status),
        Current_version = peer.CurrentVersion,
        Sort_key = peer.SortKey,
        Text_short = TextShort(peer.Text, LinkPeerTextShortMaxLength),
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
