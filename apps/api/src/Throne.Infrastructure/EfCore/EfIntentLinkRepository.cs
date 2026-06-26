using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Events;
using Throne.Domain.Intents.Linking;
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

        var missing = await FindMissingEndpointAsync(ctx, link.FromId, link.ToId, ct);
        if (missing is not null)
        {
            return new CreateIntentLinkOutcome.IntentNotFound(missing);
        }

        var existing = await FindEdgeAsync(ctx, link.FromId, link.ToId, ct);
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
        var existing = await FindEdgeAsync(ctx, fromId, toId, ct);
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
            return await ProjectAsync(ctx, intentId, rows, c);
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

            var peerIds = new HashSet<string>(StringComparer.Ordinal);
            var queriedSet = ids.ToHashSet(StringComparer.Ordinal);
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

            var peersById = await LoadPeersAsync(ctx, peerIds, c);
            var grouped = new Dictionary<string, List<IntentLinkView>>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                // Orphan edges (peer deleted) drop out of the projection.
                if (!peersById.ContainsKey(row.FromId) || !peersById.ContainsKey(row.ToId))
                {
                    continue;
                }
                if (queriedSet.Contains(row.FromId))
                {
                    AppendView(grouped, row.FromId, IntentLinkRowMapper.ToDomain(row), IntentLinkDirection.Outgoing, IntentRowMapper.ToDomain(peersById[row.ToId]));
                }
                if (queriedSet.Contains(row.ToId))
                {
                    AppendView(grouped, row.ToId, IntentLinkRowMapper.ToDomain(row), IntentLinkDirection.Incoming, IntentRowMapper.ToDomain(peersById[row.FromId]));
                }
            }
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
            var items = await ProjectAsync(ctx, intentId, pageRows, c);

            string? next = null;
            if (hasMore && pageRows.Count > 0)
            {
                var last = pageRows[^1];
                next = IntentLinkCursor.Encode(last.CreatedAt.UtcDateTime, last.Id);
            }
            return new IntentLinksPage(items, next);
        }, ct);

    private static async Task<List<IntentLinkView>> ProjectAsync(
        ThroneDbContext ctx,
        IntentId intentId,
        List<IntentLinkRow> rows,
        CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return [];
        }
        var peerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in rows)
        {
            peerIds.Add(string.Equals(r.FromId, intentId.Value, StringComparison.Ordinal) ? r.ToId : r.FromId);
        }
        var peersById = await LoadPeersAsync(ctx, peerIds, ct);
        var result = new List<IntentLinkView>(rows.Count);
        foreach (var r in rows)
        {
            var direction = string.Equals(r.FromId, intentId.Value, StringComparison.Ordinal)
                ? IntentLinkDirection.Outgoing
                : IntentLinkDirection.Incoming;
            var peerId = direction == IntentLinkDirection.Outgoing ? r.ToId : r.FromId;
            if (peersById.TryGetValue(peerId, out var peer))
            {
                result.Add(new IntentLinkView(
                    IntentLinkRowMapper.ToDomain(r),
                    direction,
                    IntentRowMapper.ToDomain(peer)));
            }
        }
        return result;
    }

    private static async Task<Dictionary<string, IntentRow>> LoadPeersAsync(
        ThroneDbContext ctx,
        HashSet<string> peerIds,
        CancellationToken ct)
    {
        if (peerIds.Count == 0)
        {
            return new Dictionary<string, IntentRow>(StringComparer.Ordinal);
        }
        var rows = await ctx.Set<IntentRow>()
            .Where(r => peerIds.Contains(r.Id))
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Id, r => r, StringComparer.Ordinal);
    }

    private static void AppendView(
        Dictionary<string, List<IntentLinkView>> bucket,
        string ownerId,
        IntentLink link,
        IntentLinkDirection direction,
        Intent peer)
    {
        if (!bucket.TryGetValue(ownerId, out var list))
        {
            list = [];
            bucket[ownerId] = list;
        }
        list.Add(new IntentLinkView(link, direction, peer));
    }

    private static async Task<string?> FindMissingEndpointAsync(
        ThroneDbContext ctx,
        IntentId fromId,
        IntentId toId,
        CancellationToken ct)
    {
        var from = fromId.Value;
        var to = toId.Value;
        var found = await ctx.Set<IntentRow>()
            .Where(r => r.Id == from || r.Id == to)
            .Select(r => r.Id)
            .ToListAsync(ct);
        var set = found.ToHashSet(StringComparer.Ordinal);
        if (!set.Contains(from))
        {
            return from;
        }
        if (!set.Contains(to))
        {
            return to;
        }
        return null;
    }

    private static Task<IntentLinkRow?> FindEdgeAsync(
        ThroneDbContext ctx,
        IntentId fromId,
        IntentId toId,
        CancellationToken ct)
    {
        var from = fromId.Value;
        var to = toId.Value;
        return ctx.Set<IntentLinkRow>()
            .FirstOrDefaultAsync(r => r.FromId == from && r.ToId == to, ct);
    }

    private ThroneDbContext RequireWriteContext(string method) =>
        Sessions.Current
            ?? throw new InvalidOperationException(
                $"EfIntentLinkRepository.{method} must run inside IUnitOfWork.ExecuteAsync.");
}
