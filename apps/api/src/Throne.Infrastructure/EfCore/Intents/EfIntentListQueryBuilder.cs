using Microsoft.EntityFrameworkCore;
using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Intents;

internal static class EfIntentListQueryBuilder
{
    private const string TaggedIntentIdsSql =
        "SELECT i.id AS Value FROM " + EfTableNames.Intents.Table + " i "
        + "WHERE EXISTS (SELECT 1 FROM json_each(i.tag_ids) WHERE value = {0})";

    private const string UntaggedIntentIdsSql =
        "SELECT i.id AS Value FROM " + EfTableNames.Intents.Table + " i "
        + "WHERE NOT EXISTS (SELECT 1 FROM json_each(i.tag_ids))";

    /// <summary>
    /// Builds the ordered, filtered query for one page. Returns <c>null</c> when an
    /// upstream filter (notably the Pinned sub-query) has determined the page is empty.
    /// </summary>
    public static async Task<IQueryable<IntentRow>?> BuildAsync(
        ThroneDbContext ctx,
        IntentListSpec spec,
        CancellationToken ct)
    {
        var query = ctx.Set<IntentRow>().AsQueryable();
        query = ApplyStatusFilter(query, spec);
        query = await ApplyTagFiltersAsync(ctx, query, spec, ct);
        query = ApplyIdFilter(query, spec);
        var (afterPinned, empty) = await ApplyPinnedFilterAsync(ctx, query, spec, ct);
        if (empty)
        {
            return null;
        }
        query = afterPinned;

        if (spec.Cursor is not null)
        {
            query = ApplyCursor(query, spec.Sort, spec.Cursor);
        }

        return ApplySort(query, spec.Sort);
    }

    public static IntentListCursor BuildNextCursor(IntentListSort sort, IntentRow row) => sort switch
    {
        IntentListSort.SortKeyAsc => new IntentListCursor(DateTimeOffset.MinValue, row.Id, row.SortKey),
        IntentListSort.UpdatedDesc => new IntentListCursor(row.UpdatedAt, row.Id),
        IntentListSort.CreatedDesc => new IntentListCursor(row.CreatedAt, row.Id),
        IntentListSort.CreatedAsc => new IntentListCursor(row.CreatedAt, row.Id),
        _ => throw new InvalidOperationException($"Unknown sort: {sort}"),
    };

    private static IQueryable<IntentRow> ApplyStatusFilter(IQueryable<IntentRow> query, IntentListSpec spec)
    {
        if (spec.Statuses is not { Count: > 0 })
        {
            return query;
        }
        var statuses = spec.Statuses;
        // Legacy rows may have empty status — treat them as "draft".
        var includeDraft = statuses.Contains(IntentStatusNames.Draft, StringComparer.Ordinal);
        return includeDraft
            ? query.Where(r => statuses.Contains(r.Status) || r.Status == string.Empty)
            : query.Where(r => statuses.Contains(r.Status));
    }

    private static async Task<IQueryable<IntentRow>> ApplyTagFiltersAsync(
        ThroneDbContext ctx,
        IQueryable<IntentRow> query,
        IntentListSpec spec,
        CancellationToken ct)
    {
        if (spec.TagId is not null && spec.Untagged)
        {
            return query.Where(_ => false);
        }

        if (spec.TagId is not null)
        {
            var taggedIds = await ctx.Database
                .SqlQueryRaw<string>(TaggedIntentIdsSql, spec.TagId.Value.Value)
                .ToListAsync(ct);
            return taggedIds.Count == 0
                ? query.Where(_ => false)
                : query.Where(r => taggedIds.Contains(r.Id));
        }
        if (spec.Untagged)
        {
            var untaggedIds = await ctx.Database
                .SqlQueryRaw<string>(UntaggedIntentIdsSql)
                .ToListAsync(ct);
            return untaggedIds.Count == 0
                ? query.Where(_ => false)
                : query.Where(r => untaggedIds.Contains(r.Id));
        }
        return query;
    }

    private static IQueryable<IntentRow> ApplyIdFilter(IQueryable<IntentRow> query, IntentListSpec spec)
    {
        if (spec.Ids is not { Count: > 0 })
        {
            return query;
        }
        var ids = spec.Ids;
        return query.Where(r => ids.Contains(r.Id));
    }

    private static async Task<(IQueryable<IntentRow> Query, bool Empty)> ApplyPinnedFilterAsync(
        ThroneDbContext ctx,
        IQueryable<IntentRow> query,
        IntentListSpec spec,
        CancellationToken ct)
    {
        if (!spec.Pinned)
        {
            return (query, false);
        }
        var pinnedIds = await ctx.Set<IntentPinRow>()
            .Select(p => p.IntentId)
            .Distinct()
            .ToListAsync(ct);
        return pinnedIds.Count == 0
            ? (query, true)
            : (query.Where(r => pinnedIds.Contains(r.Id)), false);
    }

    private static IQueryable<IntentRow> ApplySort(IQueryable<IntentRow> query, IntentListSort sort) => sort switch
    {
        IntentListSort.SortKeyAsc => query.OrderBy(r => r.SortKey).ThenBy(r => r.Id),
        IntentListSort.UpdatedDesc => query.OrderByDescending(r => r.UpdatedAt).ThenBy(r => r.Id),
        IntentListSort.CreatedDesc => query.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id),
        IntentListSort.CreatedAsc => query.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id),
        _ => throw new InvalidOperationException($"Unknown sort: {sort}"),
    };

    private static IQueryable<IntentRow> ApplyCursor(
        IQueryable<IntentRow> query,
        IntentListSort sort,
        IntentListCursor cursor) => sort switch
        {
            IntentListSort.SortKeyAsc => ApplySortKeyCursor(query, cursor),
            IntentListSort.UpdatedDesc => ApplyUpdatedDescCursor(query, cursor),
            IntentListSort.CreatedDesc => ApplyCreatedDescCursor(query, cursor),
            IntentListSort.CreatedAsc => ApplyCreatedAscCursor(query, cursor),
            _ => throw new InvalidOperationException($"Unknown sort: {sort}"),
        };

    private static IQueryable<IntentRow> ApplySortKeyCursor(IQueryable<IntentRow> query, IntentListCursor cursor)
    {
        var sortKey = cursor.SortKey ?? string.Empty;
        var id = cursor.Id;
        return query.Where(r => r.SortKey.CompareTo(sortKey) > 0
            || (r.SortKey == sortKey && r.Id.CompareTo(id) > 0));
    }

    private static IQueryable<IntentRow> ApplyUpdatedDescCursor(IQueryable<IntentRow> query, IntentListCursor cursor)
    {
        var sortValue = cursor.SortValue;
        var cid = cursor.Id;
        return query.Where(r => r.UpdatedAt < sortValue
            || (r.UpdatedAt == sortValue && r.Id.CompareTo(cid) > 0));
    }

    private static IQueryable<IntentRow> ApplyCreatedDescCursor(IQueryable<IntentRow> query, IntentListCursor cursor)
    {
        var sortValue = cursor.SortValue;
        var cid = cursor.Id;
        return query.Where(r => r.CreatedAt < sortValue
            || (r.CreatedAt == sortValue && r.Id.CompareTo(cid) > 0));
    }

    private static IQueryable<IntentRow> ApplyCreatedAscCursor(IQueryable<IntentRow> query, IntentListCursor cursor)
    {
        var sortValue = cursor.SortValue;
        var cid = cursor.Id;
        return query.Where(r => r.CreatedAt > sortValue
            || (r.CreatedAt == sortValue && r.Id.CompareTo(cid) > 0));
    }

}
