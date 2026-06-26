using Microsoft.EntityFrameworkCore;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Bindings;

/// <summary>
/// Read-only surface for <see cref="IntentRepositoryBinding"/> rows: per-id, per-intent,
/// per-clone-status, per-PR-state filtered listings. Ordering mirrors the Mongo store so the
/// «oldest poll wins» and «recovery is stable across restarts» semantics are identical.
/// </summary>
internal sealed class EfBindingReader(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions)
{
    public Task<IntentRepositoryBinding?> GetByIdAsync(BindingId id, CancellationToken ct) =>
        ReadAsync(async (ctx, c) =>
        {
            var wire = id.Value;
            var row = await ctx.Set<IntentRepositoryBindingRow>().AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == wire, c);
            return row is null ? null : IntentRepositoryBindingRowMapper.ToDomain(row);
        }, ct);

    public Task<IReadOnlyList<IntentRepositoryBinding>> FindByIntentAsync(
        IntentId intentId, CancellationToken ct) =>
        ReadAsync<IReadOnlyList<IntentRepositoryBinding>>(async (ctx, c) =>
        {
            var wire = intentId.Value;
            var rows = await ctx.Set<IntentRepositoryBindingRow>().AsNoTracking()
                .Where(r => r.IntentId == wire)
                .OrderBy(r => r.CreatedAt)
                .ToListAsync(c);
            return rows.Select(IntentRepositoryBindingRowMapper.ToDomain).ToList();
        }, ct);

    public Task<IReadOnlyList<IntentRepositoryBinding>> FindAllAsync(CancellationToken ct) =>
        ReadAsync<IReadOnlyList<IntentRepositoryBinding>>(async (ctx, c) =>
        {
            var rows = await ctx.Set<IntentRepositoryBindingRow>().AsNoTracking()
                .OrderBy(r => r.CreatedAt)
                .ToListAsync(c);
            return rows.Select(IntentRepositoryBindingRowMapper.ToDomain).ToList();
        }, ct);

    public Task<IReadOnlyList<IntentRepositoryBinding>> FindOpenForSyncAsync(CancellationToken ct) =>
        ReadAsync<IReadOnlyList<IntentRepositoryBinding>>(async (ctx, c) =>
        {
            // Ascending by LastSyncedAt: SQLite sorts NULLs first (same as Mongo), so bindings
            // that have never been polled go first — matches the «oldest poll wins» policy.
            var rows = await ctx.Set<IntentRepositoryBindingRow>().AsNoTracking()
                .Where(r => r.CloneStatus == CloneStatusNames.Ready
                    && r.PullRequestNumber != null
                    && (r.PullRequestState == PullRequestStateNames.Open || r.PullRequestState == null))
                .OrderBy(r => r.LastSyncedAt)
                .ToListAsync(c);
            return rows.Select(IntentRepositoryBindingRowMapper.ToDomain).ToList();
        }, ct);

    public Task<IReadOnlyList<IntentRepositoryBinding>> FindReadyWithoutPullRequestAsync(CancellationToken ct) =>
        ReadAsync<IReadOnlyList<IntentRepositoryBinding>>(async (ctx, c) =>
        {
            var rows = await ctx.Set<IntentRepositoryBindingRow>().AsNoTracking()
                .Where(r => r.CloneStatus == CloneStatusNames.Ready && r.PullRequestNumber == null)
                .OrderBy(r => r.UpdatedAt)
                .ToListAsync(c);
            return rows.Select(IntentRepositoryBindingRowMapper.ToDomain).ToList();
        }, ct);

    public Task<IReadOnlyList<IntentRepositoryBinding>> FindByCloneStatusAsync(
        string cloneStatus, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cloneStatus);
        return ReadAsync<IReadOnlyList<IntentRepositoryBinding>>(async (ctx, c) =>
        {
            var rows = await ctx.Set<IntentRepositoryBindingRow>().AsNoTracking()
                .Where(r => r.CloneStatus == cloneStatus)
                .OrderBy(r => r.CreatedAt)
                .ToListAsync(c);
            return rows.Select(IntentRepositoryBindingRowMapper.ToDomain).ToList();
        }, ct);
    }
}
