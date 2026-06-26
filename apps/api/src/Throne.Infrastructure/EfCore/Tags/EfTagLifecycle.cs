using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Tags;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Infrastructure.EfCore.Tags;

/// <summary>
/// Tag read + insert surface (Find/Get/Ensure/Create). Rename and SetDefaultRepositories
/// live in <see cref="EfTagMutator"/> so neither type bumps into the per-type LOC budget.
/// Duplicate-name races are resolved by catching SQLite UNIQUE conflict (error code
/// <see cref="SqliteUniqueErrorCode"/>) and re-reading the row, matching Mongo's
/// duplicate-key recovery path.
/// </summary>
internal sealed class EfTagLifecycle(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions)
{
    // SQLite extended result code for "UNIQUE constraint failed" — exposed by Microsoft.Data.Sqlite
    // as SqliteException.SqliteErrorCode == 19 (primary) with extended code 2067. We treat any
    // primary code 19 as a uniqueness conflict because the violated index is identified by name
    // ("name_unique") via the constraint we created.
    private const int SqliteConstraintErrorCode = 19;
    private const int SqliteUniqueErrorCode = 2067;

    public Task<Tag?> GetByIdAsync(TagId id, CancellationToken ct) =>
        ReadAsync(async (ctx, c) =>
        {
            var wire = id.Value;
            var row = await ctx.Set<TagRow>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == wire, c);
            return row is null ? null : TagRowMapper.ToDomain(row);
        }, ct);

    public Task<Tag?> FindByNameAsync(string normalizedName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(normalizedName);
        return ReadAsync(async (ctx, c) =>
        {
            var row = await ctx.Set<TagRow>().AsNoTracking().FirstOrDefaultAsync(r => r.Name == normalizedName, c);
            return row is null ? null : TagRowMapper.ToDomain(row);
        }, ct);
    }

    public Task<IReadOnlyList<Tag>> ListAllAsync(CancellationToken ct) =>
        ReadAsync<IReadOnlyList<Tag>>(async (ctx, c) =>
        {
            var rows = await ctx.Set<TagRow>().AsNoTracking().OrderBy(r => r.Name).ToListAsync(c);
            return rows.Select(TagRowMapper.ToDomain).ToList();
        }, ct);

    public async Task<EnsureTagOutcome> EnsureByNameAsync(string normalizedName, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(normalizedName);

        var existing = await FindByNameAsync(normalizedName, ct);
        if (existing is not null)
        {
            return new EnsureTagOutcome.Existed(existing);
        }

        var ctx = RequireContext(nameof(EnsureByNameAsync));
        var tag = Tag.Create(TagId.New(), normalizedName, now);
        ctx.Set<TagRow>().Add(TagRowMapper.ToRow(tag));

        try
        {
            await ctx.SaveChangesAsync(ct);
            return new EnsureTagOutcome.Created(tag);
        }
        catch (DbUpdateException ex) when (IsUniqueConflict(ex))
        {
            DetachAll<TagRow>(ctx);
            var raced = await ctx.Set<TagRow>().AsNoTracking()
                .FirstOrDefaultAsync(r => r.Name == normalizedName, ct)
                ?? throw new InvalidOperationException(
                    $"Tag '{normalizedName}' UNIQUE conflict but FindByNameAsync returned null.", ex);
            return new EnsureTagOutcome.Existed(TagRowMapper.ToDomain(raced));
        }
    }

    public async Task<CreateTagOutcome> CreateAsync(string rawName, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(rawName);
        var ctx = RequireContext(nameof(CreateAsync));

        var normalized = TagNames.Normalize(rawName);
        var existing = await ctx.Set<TagRow>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == normalized, ct);
        if (existing is not null)
        {
            return new CreateTagOutcome.NameTaken(TagRowMapper.ToDomain(existing));
        }

        var tag = Tag.Create(TagId.New(), normalized, now);
        ctx.Set<TagRow>().Add(TagRowMapper.ToRow(tag));
        try
        {
            await ctx.SaveChangesAsync(ct);
            return new CreateTagOutcome.Created(tag);
        }
        catch (DbUpdateException ex) when (IsUniqueConflict(ex))
        {
            DetachAll<TagRow>(ctx);
            var raced = await ctx.Set<TagRow>().AsNoTracking()
                .FirstOrDefaultAsync(r => r.Name == normalized, ct)
                ?? throw new InvalidOperationException(
                    $"Tag '{normalized}' UNIQUE conflict but lookup returned null.", ex);
            return new CreateTagOutcome.NameTaken(TagRowMapper.ToDomain(raced));
        }
    }

    private ThroneDbContext RequireContext(string method) =>
        Sessions.Current
            ?? throw new InvalidOperationException(
                $"EfTagLifecycle.{method} must run inside IUnitOfWork.ExecuteAsync.");

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

    private static void DetachAll<TEntity>(ThroneDbContext ctx) where TEntity : class
    {
        // After a failed INSERT we keep the Added entries in the change tracker; detach them so a
        // subsequent SaveChanges (e.g. status-change emission later in the same UoW) doesn't retry.
        foreach (var entry in ctx.ChangeTracker.Entries<TEntity>().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
