using Microsoft.EntityFrameworkCore;
using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Intents;

internal sealed class EfIntentReader(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions)
{
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
        return ReadAsync(async (ctx, c) =>
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
    }

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
