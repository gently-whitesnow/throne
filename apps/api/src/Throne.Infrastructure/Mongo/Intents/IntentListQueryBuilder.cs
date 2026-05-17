using MongoDB.Driver;
using Throne.Application.Intents;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Intents;

internal static class IntentListQueryBuilder
{
    public static SortDefinition<IntentDocument> BuildSort(IntentListSort sort)
    {
        var sb = Builders<IntentDocument>.Sort;
        return sort switch
        {
            IntentListSort.SortKeyAsc => sb.Combine(sb.Ascending(d => d.SortKey), sb.Ascending(d => d.Id)),
            IntentListSort.UpdatedDesc => sb.Combine(sb.Descending(d => d.UpdatedAt), sb.Ascending(d => d.Id)),
            IntentListSort.CreatedDesc => sb.Combine(sb.Descending(d => d.CreatedAt), sb.Ascending(d => d.Id)),
            IntentListSort.CreatedAsc => sb.Combine(sb.Ascending(d => d.CreatedAt), sb.Ascending(d => d.Id)),
            _ => throw new InvalidOperationException($"Unknown sort: {sort}"),
        };
    }

    public static FilterDefinition<IntentDocument> BuildCursorFilter(IntentListSort sort, IntentListCursor cursor)
    {
        var fb = Builders<IntentDocument>.Filter;
        if (sort == IntentListSort.SortKeyAsc)
        {
            var sortKey = cursor.SortKey ?? string.Empty;
            return fb.Or(
                fb.Gt(d => d.SortKey, sortKey),
                fb.And(fb.Eq(d => d.SortKey, sortKey), fb.Gt(d => d.Id, cursor.Id)));
        }
        var sortValue = cursor.SortValue.UtcDateTime;
        return sort switch
        {
            IntentListSort.UpdatedDesc => fb.Or(
                fb.Lt(d => d.UpdatedAt, sortValue),
                fb.And(fb.Eq(d => d.UpdatedAt, sortValue), fb.Gt(d => d.Id, cursor.Id))),
            IntentListSort.CreatedDesc => fb.Or(
                fb.Lt(d => d.CreatedAt, sortValue),
                fb.And(fb.Eq(d => d.CreatedAt, sortValue), fb.Gt(d => d.Id, cursor.Id))),
            IntentListSort.CreatedAsc => fb.Or(
                fb.Gt(d => d.CreatedAt, sortValue),
                fb.And(fb.Eq(d => d.CreatedAt, sortValue), fb.Gt(d => d.Id, cursor.Id))),
            _ => throw new InvalidOperationException($"Unknown sort: {sort}"),
        };
    }

    public static IntentListCursor BuildNextCursor(IntentListSort sort, IntentDocument doc) => sort switch
    {
        IntentListSort.SortKeyAsc => new IntentListCursor(DateTimeOffset.MinValue, doc.Id, doc.SortKey),
        IntentListSort.UpdatedDesc => new IntentListCursor(
            new DateTimeOffset(DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc)), doc.Id),
        IntentListSort.CreatedDesc => new IntentListCursor(
            new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)), doc.Id),
        IntentListSort.CreatedAsc => new IntentListCursor(
            new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)), doc.Id),
        _ => throw new InvalidOperationException($"Unknown sort: {sort}"),
    };
}
