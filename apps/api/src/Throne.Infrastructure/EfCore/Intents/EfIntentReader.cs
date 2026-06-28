using Microsoft.EntityFrameworkCore;
using Throne.Application.Intents;
using Throne.Application.Search;
using Throne.Domain.Intents;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Intents;

internal sealed class EfIntentReader(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions,
    IIntentSearchReader search)
    : EfRepositoryBase(contextFactory, sessions)
{
    // When a text query is combined with structural filters (status / tag / pinned …) the
    // post-rank filtering can thin the page, so we rank a wider candidate pool first. Plain
    // text search (the autocomplete case) needs exactly one page.
    private const int FilteredSearchCandidatePool = 200;

    public Task<Intent?> GetByIdAsync(IntentId id, CancellationToken ct) =>
        ReadAsync(async (ctx, c) =>
        {
            var wire = id.Value;
            var row = await ctx.Set<IntentRow>().FirstOrDefaultAsync(r => r.Id == wire, c);
            return row is null ? null : IntentRowMapper.ToDomain(row);
        }, ct);

    public Task<Intent?> GetByIdForSystemAsync(IntentId id, CancellationToken ct) =>
        GetByIdAsync(id, ct);

    public Task<IReadOnlyList<Intent>> ListAsync(IReadOnlyList<string>? statuses, CancellationToken ct) =>
        ReadAsync<IReadOnlyList<Intent>>(async (ctx, c) =>
        {
            var query = ctx.Set<IntentRow>().AsQueryable();
            if (statuses is { Count: > 0 })
            {
                var s = statuses;
                query = query.Where(r => s.Contains(r.Status));
            }
            var rows = await query.OrderBy(r => r.SortKey).ToListAsync(c);
            return rows.Select(IntentRowMapper.ToDomain).ToList();
        }, ct);

    public Task<IntentListPage> ListPagedAsync(IntentListSpec spec, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return spec.Query is null
            ? ListUnrankedAsync(spec, ct)
            : SearchRankedAsync(spec, ct);
    }

    private Task<IntentListPage> ListUnrankedAsync(IntentListSpec spec, CancellationToken ct) =>
        ReadAsync(async (ctx, c) =>
        {
            var query = await EfIntentListQueryBuilder.BuildAsync(ctx, spec, c);
            if (query is null)
            {
                return new IntentListPage([], NextCursor: null);
            }

            var pageSize = spec.Limit + 1;
            var rows = await query.Take(pageSize).ToListAsync(c);
            var hasMore = rows.Count > spec.Limit;
            var pageRows = hasMore ? rows.Take(spec.Limit).ToList() : rows;

            var items = pageRows.Select(IntentRowMapper.ToDomain).ToList();
            IntentListCursor? next = null;
            if (hasMore && pageRows.Count > 0)
            {
                next = EfIntentListQueryBuilder.BuildNextCursor(spec.Sort, pageRows[^1]);
            }
            return new IntentListPage(items, next);
        }, ct);

    // Ranked query path (ADR-0050): FTS5/BM25 produces the order, the existing structural
    // filters narrow it, and the page is returned in rank order with highlighted snippets.
    // No cursor — ranked search is single-page for now (the autocomplete consumer reads the
    // first page only).
    private async Task<IntentListPage> SearchRankedAsync(IntentListSpec spec, CancellationToken ct)
    {
        var pool = HasStructuralFilters(spec) ? FilteredSearchCandidatePool : spec.Limit;
        var hits = await search.SearchAsync(spec.Query!, pool, ct);
        if (hits.Count == 0)
        {
            return new IntentListPage([], NextCursor: null);
        }

        var snippetById = new Dictionary<string, string>(StringComparer.Ordinal);
        var rankedIds = new List<string>(hits.Count);
        foreach (var hit in hits)
        {
            if (snippetById.TryAdd(hit.IntentId, hit.Snippet))
            {
                rankedIds.Add(hit.IntentId);
            }
        }

        List<string> effectiveIds = rankedIds;
        if (spec.Ids is { Count: > 0 })
        {
            var allowed = spec.Ids.ToHashSet(StringComparer.Ordinal);
            effectiveIds = rankedIds.Where(allowed.Contains).ToList();
        }
        if (effectiveIds.Count == 0)
        {
            return new IntentListPage([], NextCursor: null);
        }

        return await ReadAsync(async (ctx, c) =>
        {
            var filterSpec = spec with { Query = null, Cursor = null, Ids = effectiveIds };
            var query = await EfIntentListQueryBuilder.BuildAsync(ctx, filterSpec, c);
            if (query is null)
            {
                return new IntentListPage([], NextCursor: null);
            }

            var rowsById = (await query.ToListAsync(c)).ToDictionary(r => r.Id, StringComparer.Ordinal);
            var items = new List<Intent>(Math.Min(spec.Limit, rowsById.Count));
            var snippets = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var id in rankedIds)
            {
                if (items.Count >= spec.Limit)
                {
                    break;
                }
                if (rowsById.TryGetValue(id, out var row))
                {
                    items.Add(IntentRowMapper.ToDomain(row));
                    snippets[id] = snippetById[id];
                }
            }

            return new IntentListPage(items, NextCursor: null, Snippets: snippets);
        }, ct);
    }

    private static bool HasStructuralFilters(IntentListSpec spec) =>
        spec.Statuses is { Count: > 0 }
        || spec.TagId is not null
        || spec.Untagged
        || spec.Pinned
        || spec.Ids is { Count: > 0 };

    public Task<string?> GetMinSortKeyAsync(CancellationToken ct) =>
        ReadAsync(async (ctx, c) =>
        {
            var key = await ctx.Set<IntentRow>()
                .OrderBy(r => r.SortKey)
                .Select(r => r.SortKey)
                .FirstOrDefaultAsync(c);
            return string.IsNullOrEmpty(key) ? null : key;
        }, ct);
}
