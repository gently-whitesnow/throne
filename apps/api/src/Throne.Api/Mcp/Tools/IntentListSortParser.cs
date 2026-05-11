using Throne.Application.Intents;

namespace Throne.Api.Mcp.Tools;

internal static class IntentListSortParser
{
    public static IntentListSort Parse(string? raw) => raw switch
    {
        null or "" or "sort_key_asc" => IntentListSort.SortKeyAsc,
        "updated_desc" => IntentListSort.UpdatedDesc,
        "created_desc" => IntentListSort.CreatedDesc,
        "created_asc" => IntentListSort.CreatedAsc,
        _ => throw new ArgumentException(
            $"Unknown sort '{raw}'. Allowed: sort_key_asc, updated_desc, created_desc, created_asc.",
            nameof(raw)),
    };
}
