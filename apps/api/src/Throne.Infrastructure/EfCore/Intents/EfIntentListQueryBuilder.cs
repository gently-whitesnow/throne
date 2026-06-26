using Microsoft.EntityFrameworkCore;
using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Intents;

internal static class EfIntentListQueryBuilder
{
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

        if (spec.Statuses is { Count: > 0 })
        {
            var statuses = spec.Statuses;
            // Legacy rows may have empty status — treat them as "draft" the same way the
            // Mongo filter does so the two backends list identically.
            var includeDraft = statuses.Contains(IntentStatusNames.Draft, StringComparer.Ordinal);
            query = includeDraft
                ? query.Where(r => statuses.Contains(r.Status) || r.Status == string.Empty)
                : query.Where(r => statuses.Contains(r.Status));
        }

        if (spec.TagId is not null)
        {
            // JSON array LIKE: tag ids are GUID hex (no quote chars), so a simple contains
            // probe stays sound. Mirrors the Mongo AnyEq semantics without a join table.
            var probe = $"%\"{spec.TagId.Value.Value}\"%";
            query = query.Where(r => EF.Functions.Like(EF.Property<string>(r, "tag_ids"), probe));
        }

        if (spec.Untagged)
        {
            query = query.Where(r => EF.Property<string>(r, "tag_ids") == "[]");
        }

        if (spec.Ids is { Count: > 0 })
        {
            var ids = spec.Ids;
            query = query.Where(r => ids.Contains(r.Id));
        }

        if (spec.Pinned)
        {
            var pinnedIds = await ctx.Set<IntentPinRow>()
                .Select(p => p.IntentId)
                .Distinct()
                .ToListAsync(ct);
            if (pinnedIds.Count == 0)
            {
                return null;
            }
            query = query.Where(r => pinnedIds.Contains(r.Id));
        }

        if (!string.IsNullOrEmpty(spec.Query))
        {
            // AND of per-token LIKE %word%. Tokenisation is whitespace-split + non-empty,
            // matching the Mongo regex’s «every whitespace-separated chunk must match».
            foreach (var token in TokenizeQuery(spec.Query))
            {
                var pattern = $"%{Escape(token)}%";
                query = query.Where(r => EF.Functions.Like(r.Text, pattern));
            }
        }

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
        IntentListCursor cursor)
    {
        if (sort == IntentListSort.SortKeyAsc)
        {
            var sortKey = cursor.SortKey ?? string.Empty;
            var id = cursor.Id;
            return query.Where(r => string.Compare(r.SortKey, sortKey, StringComparison.Ordinal) > 0
                || (r.SortKey == sortKey && string.Compare(r.Id, id, StringComparison.Ordinal) > 0));
        }
        var sortValue = cursor.SortValue;
        var cid = cursor.Id;
        return sort switch
        {
            IntentListSort.UpdatedDesc => query.Where(r => r.UpdatedAt < sortValue
                || (r.UpdatedAt == sortValue && string.Compare(r.Id, cid, StringComparison.Ordinal) > 0)),
            IntentListSort.CreatedDesc => query.Where(r => r.CreatedAt < sortValue
                || (r.CreatedAt == sortValue && string.Compare(r.Id, cid, StringComparison.Ordinal) > 0)),
            IntentListSort.CreatedAsc => query.Where(r => r.CreatedAt > sortValue
                || (r.CreatedAt == sortValue && string.Compare(r.Id, cid, StringComparison.Ordinal) > 0)),
            _ => throw new InvalidOperationException($"Unknown sort: {sort}"),
        };
    }

    private static readonly char[] QueryWhitespace = [' ', '\t', '\r', '\n'];

    private static IEnumerable<string> TokenizeQuery(string query)
    {
        foreach (var token in query.Split(QueryWhitespace, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!string.IsNullOrEmpty(token))
            {
                yield return token;
            }
        }
    }

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
