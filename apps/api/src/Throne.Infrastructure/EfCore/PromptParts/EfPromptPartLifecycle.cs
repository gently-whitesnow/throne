using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.PromptParts;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.PromptParts;

/// <summary>
/// Read surface + create/delete writes for prompt parts. Create inserts the part row plus
/// the v1 text-version snapshot transactionally; delete is the CAS-free
/// «no version arg» variant matching the port — the aggregate guards via
/// the «has roles» short-circuit.
/// </summary>
internal sealed class EfPromptPartLifecycle(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions)
{
    // SQLite returns primary code 19 for any constraint violation; extended 2067 narrows it
    // to UNIQUE specifically. We accept either to stay resilient to driver-version drift.
    private const int SqliteConstraintErrorCode = 19;
    private const int SqliteUniqueErrorCode = 2067;

    public Task<PromptPart?> GetByIdAsync(PromptPartId id, CancellationToken ct) =>
        ReadAsync(async (ctx, c) =>
        {
            var wire = id.Value;
            var row = await ctx.Set<PromptPartRow>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == wire, c);
            return row is null ? null : PromptPartRowMapper.ToDomain(row);
        }, ct);

    public Task<PromptPart?> GetByScopeKeyAsync(string scope, string key, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return ReadAsync(async (ctx, c) =>
        {
            var row = await ctx.Set<PromptPartRow>().AsNoTracking()
                .FirstOrDefaultAsync(r => r.Scope == scope && r.Key == key, c);
            return row is null ? null : PromptPartRowMapper.ToDomain(row);
        }, ct);
    }

    public Task<IReadOnlyList<PromptPart>> ListAsync(string? scope, CancellationToken ct) =>
        ReadAsync<IReadOnlyList<PromptPart>>(async (ctx, c) =>
        {
            var query = ctx.Set<PromptPartRow>().AsNoTracking().AsQueryable();
            if (scope is not null)
            {
                query = query.Where(r => r.Scope == scope);
            }
            var rows = await query.OrderBy(r => r.Scope).ThenBy(r => r.Key).ToListAsync(c);
            return rows.Select(PromptPartRowMapper.ToDomain).ToList();
        }, ct);

    public Task<IReadOnlyList<PromptPart>> GetByScopeAndKeysAsync(
        string scope,
        IReadOnlyList<string> keys,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<PromptPart>>([]);
        }
        return ReadAsync<IReadOnlyList<PromptPart>>(async (ctx, c) =>
        {
            var rows = await ctx.Set<PromptPartRow>().AsNoTracking()
                .Where(r => r.Scope == scope && keys.Contains(r.Key))
                .OrderBy(r => r.Key)
                .ToListAsync(c);
            return rows.Select(PromptPartRowMapper.ToDomain).ToList();
        }, ct);
    }

    public async Task<CreatePromptPartOutcome> CreateAsync(
        PromptPart part,
        TextVersion initialVersion,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(initialVersion);
        var ctx = RequireContext(nameof(CreateAsync));

        var existing = await ctx.Set<PromptPartRow>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Scope == part.Scope && r.Key == part.Key, ct);
        if (existing is not null)
        {
            return new CreatePromptPartOutcome.KeyConflict();
        }

        ctx.Set<PromptPartRow>().Add(PromptPartRowMapper.ToRow(part));
        ctx.Set<TextVersionRow>().Add(TextVersionRowMapper.ToRow(initialVersion));
        try
        {
            await ctx.SaveChangesAsync(ct);
            return new CreatePromptPartOutcome.Created(part);
        }
        catch (DbUpdateException ex) when (IsUniqueConflict(ex))
        {
            DetachInserts(ctx);
            return new CreatePromptPartOutcome.KeyConflict();
        }
    }

    public async Task<DeletePromptPartOutcome> DeleteAsync(PromptPartId id, CancellationToken ct)
    {
        var ctx = RequireContext(nameof(DeleteAsync));
        var wire = id.Value;

        var row = await ctx.Set<PromptPartRow>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == wire, ct);
        if (row is null)
        {
            return new DeletePromptPartOutcome.NotFound();
        }
        if (row.ModeRoles.Count > 0)
        {
            return new DeletePromptPartOutcome.HasRoles();
        }

        var deleted = await ctx.Set<PromptPartRow>()
            .Where(r => r.Id == wire)
            .ExecuteDeleteAsync(ct);
        if (deleted == 0)
        {
            return new DeletePromptPartOutcome.NotFound();
        }
        return new DeletePromptPartOutcome.Deleted();
    }

    private ThroneDbContext RequireContext(string method) =>
        Sessions.Current
            ?? throw new InvalidOperationException(
                $"EfPromptPartLifecycle.{method} must run inside IUnitOfWork.ExecuteAsync.");

    private static bool IsUniqueConflict(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        while (inner is not null)
        {
            if (inner is SqliteException sqlite
                && (sqlite.SqliteErrorCode == SqliteConstraintErrorCode
                    || sqlite.SqliteExtendedErrorCode == SqliteUniqueErrorCode))
            {
                return true;
            }
            inner = inner.InnerException;
        }
        return false;
    }

    private static void DetachInserts(ThroneDbContext ctx)
    {
        // Failed INSERT leaves Added entries in the tracker; detach the prompt-part and
        // text-version rows so a later SaveChanges in the same UoW does not retry.
        foreach (var entry in ctx.ChangeTracker.Entries<PromptPartRow>().ToList())
        {
            entry.State = EntityState.Detached;
        }
        foreach (var entry in ctx.ChangeTracker.Entries<TextVersionRow>().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
