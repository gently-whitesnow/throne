using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// EF Core <see cref="ISkillModeDefaultStore"/>. <see cref="ReplaceAsync"/> is a per-row
/// upsert (overwrite enabled); <see cref="UpsertMissingAsync"/> is set-on-insert so the
/// boot seeder never overwrites an operator-edited default. Listing is deterministic by
/// (mode, skill_id) to keep diffs stable across backends. The store does not require an
/// ambient unit of work — the boot seeder runs before any DI-managed session.
/// </summary>
internal sealed class EfSkillModeDefaultStore(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions), ISkillModeDefaultStore
{
    private readonly IDbContextFactory<ThroneDbContext> _contextFactory = contextFactory;

    public Task<IReadOnlyList<SkillModeDefault>> ListAsync(CancellationToken ct) =>
        ReadAsync<IReadOnlyList<SkillModeDefault>>(async (ctx, c) =>
        {
            var rows = await ctx.Set<SkillModeDefaultRow>().AsNoTracking()
                .OrderBy(r => r.Mode).ThenBy(r => r.SkillId)
                .ToListAsync(c);
            return rows.Select(SkillModeDefaultRowMapper.ToDomain).ToList();
        }, ct);

    public Task ReplaceAsync(IReadOnlyList<SkillModeDefault> defaults, CancellationToken ct) =>
        UpsertAsync(defaults, overwriteExisting: true, ct);

    public Task UpsertMissingAsync(IReadOnlyList<SkillModeDefault> defaults, CancellationToken ct) =>
        UpsertAsync(defaults, overwriteExisting: false, ct);

    private async Task UpsertAsync(
        IReadOnlyList<SkillModeDefault> defaults, bool overwriteExisting, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        if (defaults.Count == 0)
        {
            return;
        }

        var ambient = Sessions.Current;
        if (ambient is not null)
        {
            await ApplyAsync(ambient, defaults, overwriteExisting, ct);
            return;
        }

        // Seeder runs outside any unit of work, so open a one-shot context for the upsert.
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        await ApplyAsync(context, defaults, overwriteExisting, ct);
    }

    private static async Task ApplyAsync(
        ThroneDbContext ctx,
        IReadOnlyList<SkillModeDefault> defaults,
        bool overwriteExisting,
        CancellationToken ct)
    {
        var ids = defaults.Select(d => SkillModeDefaultRowMapper.IdOf(d.Mode, d.SkillId)).ToHashSet();
        var existing = await ctx.Set<SkillModeDefaultRow>()
            .Where(r => ids.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, ct);

        foreach (var item in defaults)
        {
            var id = SkillModeDefaultRowMapper.IdOf(item.Mode, item.SkillId);
            if (existing.TryGetValue(id, out var row))
            {
                if (overwriteExisting)
                {
                    row.Mode = item.Mode;
                    row.SkillId = item.SkillId;
                    row.Enabled = item.Enabled;
                }
                // overwriteExisting=false → leave operator-edited rows untouched.
            }
            else
            {
                ctx.Set<SkillModeDefaultRow>().Add(SkillModeDefaultRowMapper.ToRow(item));
            }
        }
        await ctx.SaveChangesAsync(ct);
    }
}
