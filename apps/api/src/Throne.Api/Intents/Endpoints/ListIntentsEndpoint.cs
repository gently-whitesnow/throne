using Microsoft.AspNetCore.Mvc;
using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Domain.Tags;
using Throne.Intents.Contracts.Generated;
using DtoIntentListSort = Throne.Intents.Contracts.Generated.IntentListSort;

namespace Throne.Api.Intents;

public sealed class ListIntentsEndpoint(ListIntentsHandler handler, IntentsApiHelpers helpers)
{
    public const int TextShortMaxLength = 140;

    public async Task<ActionResult<IntentListPageDto>> RunAsync(
        string? cursor,
        int? limit,
        IEnumerable<IntentStatus>? status,
        string? tag,
        bool? untagged,
        string? tracker,
        string? board,
        bool? pinned,
        bool? terminalRunning,
        string? query,
        DtoIntentListSort? sort,
        CancellationToken cancellationToken)
    {
        var statuses = status?
            .Select(IntentStatusDtoMapper.FromContractStatus)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var pagedQuery = new ListIntentsPagedQuery(
            Statuses: statuses is { Length: > 0 } ? statuses : null,
            TagName: string.IsNullOrWhiteSpace(tag) ? null : tag,
            Untagged: untagged.GetValueOrDefault(),
            Pinned: pinned.GetValueOrDefault(),
            TerminalRunning: terminalRunning.GetValueOrDefault(),
            BoardTracker: string.IsNullOrWhiteSpace(tracker) ? null : tracker,
            BoardId: string.IsNullOrWhiteSpace(board) ? null : board,
            Query: string.IsNullOrWhiteSpace(query) ? null : query,
            Sort: IntentListSortDtoMapper.FromContractSort(sort),
            Limit: limit ?? ListIntentsHandler.DefaultLimit,
            Cursor: IntentListCursorCodec.Parse(cursor));

        var page = await handler.HandleAsync(pagedQuery, cancellationToken);

        var tagMap = await helpers.BuildTagMapAsync(page.Items.SelectMany(i => i.TagIds), cancellationToken);
        var pinnedMap = await helpers.GetPinnedInAsync(page.Items.Select(i => i.Id.Value).ToList(), cancellationToken);

        return new OkObjectResult(new IntentListPageDto
        {
            Items = MapItems(page.Items, tagMap, pinnedMap, page.Snippets),
            Next_cursor = IntentListCursorCodec.Encode(page.NextCursor),
        });
    }

    private static System.Collections.ObjectModel.Collection<IntentListItemDto> MapItems(
        IReadOnlyList<Intent> intents,
        IReadOnlyDictionary<string, Tag> tagMap,
        IReadOnlyDictionary<string, IReadOnlyList<IntentPin>> pinnedMap,
        IReadOnlyDictionary<string, string>? snippets)
    {
        var dtos = new System.Collections.ObjectModel.Collection<IntentListItemDto>();
        foreach (var intent in intents)
        {
            dtos.Add(IntentDtoMapper.ToListDto(
                intent,
                tagMap,
                TextShortMaxLength,
                pinnedMap.TryGetValue(intent.Id.Value, out var pinned) ? pinned : null,
                snippets?.GetValueOrDefault(intent.Id.Value)));
        }
        return dtos;
    }
}
