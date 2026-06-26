using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Events;
using Throne.Domain.Intents.Linking;
using Throne.Infrastructure.EfCore.Links;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore;

internal sealed class EfIntentLinkRepository(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions,
    IIntentEventRepository intentEvents,
    TimeProvider clock)
    : EfRepositoryBase(contextFactory, sessions), IIntentLinkRepository
{
    public async Task<CreateIntentLinkOutcome> CreateAsync(IntentLink link, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(link);
        var ctx = RequireWriteContext(nameof(CreateAsync));

        var missing = await EfIntentLinkQueries.FindMissingEndpointAsync(ctx, link.FromId, link.ToId, ct);
        if (missing is not null)
        {
            return new CreateIntentLinkOutcome.IntentNotFound(missing);
        }

        var existing = await EfIntentLinkQueries.FindEdgeAsync(ctx, link.FromId, link.ToId, ct);
        if (existing is not null)
        {
            return new CreateIntentLinkOutcome.Duplicate(IntentLinkRowMapper.ToDomain(existing));
        }

        ctx.Set<IntentLinkRow>().Add(IntentLinkRowMapper.ToRow(link));
        await ctx.SaveChangesAsync(ct);
        await intentEvents.AppendAsync(
            IntentEvent.ForLinkAdded(Guid.NewGuid().ToString("N"), link), ct);
        return new CreateIntentLinkOutcome.Created(link);
    }

    public async Task<DeleteIntentLinkOutcome> DeleteAsync(
        IntentId fromId,
        IntentId toId,
        CancellationToken ct)
    {
        var ctx = RequireWriteContext(nameof(DeleteAsync));
        var existing = await EfIntentLinkQueries.FindEdgeAsync(ctx, fromId, toId, ct);
        if (existing is null)
        {
            return new DeleteIntentLinkOutcome.NotFound();
        }

        ctx.Set<IntentLinkRow>().Remove(existing);
        await ctx.SaveChangesAsync(ct);

        var domain = IntentLinkRowMapper.ToDomain(existing);
        await intentEvents.AppendAsync(
            IntentEvent.ForLinkRemoved(Guid.NewGuid().ToString("N"), domain, clock.GetUtcNow()), ct);
        return new DeleteIntentLinkOutcome.Deleted(domain);
    }

    public Task<IReadOnlyList<IntentLinkView>> ListByIntentAsync(IntentId intentId, CancellationToken ct) =>
        ReadAsync<IReadOnlyList<IntentLinkView>>(async (ctx, c) =>
        {
            var id = intentId.Value;
            var rows = await ctx.Set<IntentLinkRow>()
                .Where(r => r.FromId == id || r.ToId == id)
                .OrderBy(r => r.CreatedAt)
                .ToListAsync(c);
            return await EfIntentLinkProjection.ProjectAsync(ctx, intentId, rows, c);
        }, ct);

    public Task<IReadOnlyDictionary<string, IReadOnlyList<IntentLinkView>>> ListByIntentsAsync(
        IReadOnlyList<IntentId> intentIds,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(intentIds);
        return ReadAsync<IReadOnlyDictionary<string, IReadOnlyList<IntentLinkView>>>(async (ctx, c) =>
        {
            var ids = intentIds.Select(i => i.Value).Distinct(StringComparer.Ordinal).ToList();
            var result = new Dictionary<string, IReadOnlyList<IntentLinkView>>(StringComparer.Ordinal);
            if (ids.Count == 0)
            {
                return result;
            }

            var rows = await ctx.Set<IntentLinkRow>()
                .Where(r => ids.Contains(r.FromId) || ids.Contains(r.ToId))
                .OrderBy(r => r.CreatedAt)
                .ToListAsync(c);

            var queriedSet = ids.ToHashSet(StringComparer.Ordinal);
            var peerIds = CollectPeerIds(rows, queriedSet, ids);
            var peersById = await EfIntentLinkProjection.LoadPeersAsync(ctx, peerIds, c);
            var grouped = GroupByOwner(rows, queriedSet, peersById);
            foreach (var (key, list) in grouped)
            {
                result[key] = list;
            }
            return result;
        }, ct);
    }

    public Task<IntentLinksPage> ListPagedAsync(
        IntentId intentId,
        IntentLinkDirection? direction,
        bool? blocking,
        int limit,
        string? cursor,
        CancellationToken ct) =>
        ReadAsync(async (ctx, c) =>
        {
            var id = intentId.Value;
            var query = ctx.Set<IntentLinkRow>().AsQueryable();
            query = direction switch
            {
                IntentLinkDirection.Outgoing => query.Where(r => r.FromId == id),
                IntentLinkDirection.Incoming => query.Where(r => r.ToId == id),
                _ => query.Where(r => r.FromId == id || r.ToId == id),
            };
            if (blocking is not null)
            {
                var b = blocking.Value;
                query = query.Where(r => r.Blocking == b);
            }
            if (!string.IsNullOrWhiteSpace(cursor))
            {
                var (cursorTimeUtc, cursorId) = IntentLinkCursor.Decode(cursor);
                var cursorTime = new DateTimeOffset(DateTime.SpecifyKind(cursorTimeUtc, DateTimeKind.Utc));
                query = query.Where(r => r.CreatedAt > cursorTime
                    || (r.CreatedAt == cursorTime && string.Compare(r.Id, cursorId, StringComparison.Ordinal) > 0));
            }

            var pageSize = limit + 1;
            var rows = await query
                .OrderBy(r => r.CreatedAt)
                .ThenBy(r => r.Id)
                .Take(pageSize)
                .ToListAsync(c);

            var hasMore = rows.Count > limit;
            var pageRows = hasMore ? rows.Take(limit).ToList() : rows;
            var items = await EfIntentLinkProjection.ProjectAsync(ctx, intentId, pageRows, c);

            string? next = null;
            if (hasMore && pageRows.Count > 0)
            {
                var last = pageRows[^1];
                next = IntentLinkCursor.Encode(last.CreatedAt.UtcDateTime, last.Id);
            }
            return new IntentLinksPage(items, next);
        }, ct);

    private static HashSet<string> CollectPeerIds(
        List<IntentLinkRow> rows,
        HashSet<string> queriedSet,
        List<string> ids)
    {
        var peerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (!queriedSet.Contains(row.FromId))
            {
                peerIds.Add(row.FromId);
            }
            if (!queriedSet.Contains(row.ToId))
            {
                peerIds.Add(row.ToId);
            }
        }
        foreach (var id in ids)
        {
            peerIds.Add(id);
        }
        return peerIds;
    }

    private static Dictionary<string, List<IntentLinkView>> GroupByOwner(
        List<IntentLinkRow> rows,
        HashSet<string> queriedSet,
        Dictionary<string, IntentRow> peersById)
    {
        var grouped = new Dictionary<string, List<IntentLinkView>>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            // Orphan edges (peer deleted) drop out of the projection.
            if (!peersById.ContainsKey(row.FromId) || !peersById.ContainsKey(row.ToId))
            {
                continue;
            }
            var link = IntentLinkRowMapper.ToDomain(row);
            if (queriedSet.Contains(row.FromId))
            {
                EfIntentLinkProjection.AppendView(grouped, row.FromId, link, IntentLinkDirection.Outgoing, IntentRowMapper.ToDomain(peersById[row.ToId]));
            }
            if (queriedSet.Contains(row.ToId))
            {
                EfIntentLinkProjection.AppendView(grouped, row.ToId, link, IntentLinkDirection.Incoming, IntentRowMapper.ToDomain(peersById[row.FromId]));
            }
        }
        return grouped;
    }

    private ThroneDbContext RequireWriteContext(string method) =>
        Sessions.Current
            ?? throw new InvalidOperationException(
                $"EfIntentLinkRepository.{method} must run inside IUnitOfWork.ExecuteAsync.");
}
