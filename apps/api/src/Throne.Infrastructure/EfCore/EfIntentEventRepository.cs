using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Events;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore;

internal sealed class EfIntentEventRepository(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions), IIntentEventRepository
{
    public Task<IReadOnlyList<IntentEvent>> ListByIntentAsync(IntentId intentId, CancellationToken ct) =>
        ReadAsync<IReadOnlyList<IntentEvent>>(async (ctx, c) =>
        {
            var id = intentId.Value;
            var rows = await ctx.Set<IntentEventRow>()
                .Where(r => r.IntentId == id || r.PeerIntentId == id)
                .OrderBy(r => r.CreatedAt)
                .ToListAsync(c);
            return rows.Select(IntentEventRowMapper.ToDomain).ToList();
        }, ct);

    public Task<IReadOnlyList<IntentEvent>> ListTextChangesAsync(IntentId intentId, CancellationToken ct) =>
        ReadAsync<IReadOnlyList<IntentEvent>>(async (ctx, c) =>
        {
            var id = intentId.Value;
            var wire = IntentEventKind.TextChanged.ToWire();
            var rows = await ctx.Set<IntentEventRow>()
                .Where(r => r.IntentId == id && r.Kind == wire)
                .OrderBy(r => r.Version)
                .ToListAsync(c);
            return rows.Select(IntentEventRowMapper.ToDomain).ToList();
        }, ct);

    public async Task AppendAsync(IntentEvent evt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (Sessions.Current is null)
        {
            // Writes need an ambient context — the read fallback would silently drop into
            // a transient context, breaking event ↔ aggregate atomicity.
            throw new InvalidOperationException(
                "EfIntentEventRepository.AppendAsync must run inside IUnitOfWork.ExecuteAsync.");
        }
        var ctx = RequireContext();
        var row = IntentEventRowMapper.ToRow(evt);
        ctx.Set<IntentEventRow>().Add(row);
        await ctx.SaveChangesAsync(ct);
    }
}
