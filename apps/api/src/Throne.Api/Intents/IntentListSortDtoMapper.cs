using AppIntentListSort = Throne.Application.Intents.IntentListSort;
using DtoIntentListSort = Throne.Intents.Contracts.Generated.IntentListSort;

namespace Throne.Api.Intents;

internal static class IntentListSortDtoMapper
{
    public static AppIntentListSort FromContractSort(DtoIntentListSort? dto) => dto switch
    {
        null => AppIntentListSort.SortKeyAsc,
        DtoIntentListSort.Sort_key_asc => AppIntentListSort.SortKeyAsc,
        DtoIntentListSort.Updated_desc => AppIntentListSort.UpdatedDesc,
        DtoIntentListSort.Created_desc => AppIntentListSort.CreatedDesc,
        DtoIntentListSort.Created_asc => AppIntentListSort.CreatedAsc,
        _ => throw new ArgumentOutOfRangeException(nameof(dto), dto, "Unknown sort value."),
    };
}
