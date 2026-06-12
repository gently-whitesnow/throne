using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Domain.Tags;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class IntentDtoMapper
{
    public static IntentDetailDto ToDetailDto(
        Intent intent,
        IReadOnlyDictionary<string, Tag> tagsById,
        IReadOnlyList<IntentLinkViewDto>? links = null,
        IReadOnlyList<IntentPin>? pinnedIn = null) => new()
        {
            Id = intent.Id.Value,
            Status = IntentStatusDtoMapper.ToContractStatus(intent.State.Status),
            Current_version = intent.State.CurrentVersion,
            Tags = IntentTagDtoMapper.ToTagRefs(intent.TagIds, tagsById),
            Text = intent.State.Text,
            Sort_key = intent.State.SortKey,
            Created_at = intent.CreatedAt,
            Updated_at = intent.State.UpdatedAt,
            Links = links is null
                ? new System.Collections.ObjectModel.Collection<IntentLinkViewDto>()
                : new System.Collections.ObjectModel.Collection<IntentLinkViewDto>([.. links]),
            Pinned_in = IntentPinDtoMapper.ToPinnedContexts(pinnedIn),
            Cleanup_local_state_on_done = intent.State.CleanupLocalStateOnDone,
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
            Tags = IntentTagDtoMapper.ToTagRefs(intent.TagIds, tagsById),
            Text_short = IntentTextSnippet.Cut(intent.State.Text, textShortMaxLength),
            Sort_key = intent.State.SortKey,
            Created_at = intent.CreatedAt,
            Updated_at = intent.State.UpdatedAt,
            Pinned_in = IntentPinDtoMapper.ToPinnedContexts(pinnedIn),
        };

    public static IntentLinkPeerDto ToPeerDto(Intent peer, IReadOnlyDictionary<string, Tag> tagsById) =>
        IntentLinkPeerDtoMapper.ToPeerDto(peer, tagsById);

    public static IntentAttachmentDto ToAttachmentDto(IntentAttachment attachment) =>
        IntentAttachmentDtoMapper.ToAttachmentDto(attachment);
}
