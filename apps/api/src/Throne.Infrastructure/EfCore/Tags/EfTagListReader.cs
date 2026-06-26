using Microsoft.EntityFrameworkCore;
using Throne.Application.Tags;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Tags;

/// <summary>
/// Keyset-paged tag reader. Sort key is <c>last_attached_at ?? created_at</c> descending
/// with <c>id</c> ascending as tiebreaker — same shape as Mongo's
/// <c>__sort_attached_at</c> computed field, projected into SQLite via
/// <see cref="DateTimeOffset?"/> coalescing in the LINQ expression.
/// </summary>
internal sealed class EfTagListReader(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions)
{
    public Task<TagListPage> ListPageAsync(TagListSpec spec, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return ReadAsync(async (ctx, c) =>
        {
            var query = ctx.Set<TagRow>().AsQueryable();
            query = ApplySearchFilter(query, spec);
            query = ApplyCursorFilter(query, spec);

            var ordered = query
                .OrderByDescending(r => r.LastAttachedAt ?? r.CreatedAt)
                .ThenBy(r => r.Id);

            var rows = await ordered.Take(spec.Limit + 1).ToListAsync(c);
            var hasMore = rows.Count > spec.Limit;
            var pageRows = hasMore ? rows.Take(spec.Limit).ToList() : rows;

            var items = pageRows.Select(r => new TagListItem(TagRowMapper.ToDomain(r))).ToList();
            var next = BuildNextCursor(hasMore, pageRows);
            return new TagListPage(items, next);
        }, ct);
    }

    private static IQueryable<TagRow> ApplySearchFilter(IQueryable<TagRow> query, TagListSpec spec)
    {
        if (string.IsNullOrEmpty(spec.Search))
        {
            return query;
        }
        // Substring match against the stored (already lowercased) name. Tag names
        // are always normalized to lowercase slugs so a plain LIKE matches what the
        // Mongo regex-with-i flag yields on identical inputs.
        var probe = $"%{Escape(spec.Search)}%";
        return query.Where(r => EF.Functions.Like(r.Name, probe));
    }

    private static IQueryable<TagRow> ApplyCursorFilter(IQueryable<TagRow> query, TagListSpec spec)
    {
        if (spec.Cursor is null)
        {
            return query;
        }
        var sortKey = DateTime.SpecifyKind(spec.Cursor.LastAttachedAt, DateTimeKind.Utc);
        var sortOffset = new DateTimeOffset(sortKey);
        var cursorId = spec.Cursor.Id;
        return query.Where(r =>
            (r.LastAttachedAt ?? r.CreatedAt) < sortOffset
            || ((r.LastAttachedAt ?? r.CreatedAt) == sortOffset
                && string.Compare(r.Id, cursorId, StringComparison.Ordinal) > 0));
    }

    private static TagListCursor? BuildNextCursor(bool hasMore, List<TagRow> pageRows)
    {
        if (!hasMore || pageRows.Count == 0)
        {
            return null;
        }
        var last = pageRows[^1];
        var sortValue = (last.LastAttachedAt ?? last.CreatedAt).UtcDateTime;
        return new TagListCursor(DateTime.SpecifyKind(sortValue, DateTimeKind.Utc), last.Id);
    }

    private static string Escape(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
