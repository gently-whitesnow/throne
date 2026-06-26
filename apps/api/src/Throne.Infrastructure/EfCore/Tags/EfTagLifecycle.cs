using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Tags;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Infrastructure.EfCore.Tags;

/// <summary>
/// Single-tag read + write surface (Find/Get/Ensure/Create/Rename/SetDefaultRepositories).
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

    public async Task<RenameTagOutcome> RenameAsync(
        TagId id,
        int expectedVersion,
        string rawName,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var ctx = RequireContext(nameof(RenameAsync));
        var wire = id.Value;

        var row = await ctx.Set<TagRow>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == wire, ct);
        if (row is null)
        {
            return new RenameTagOutcome.NotFound();
        }
        if (row.CurrentVersion != expectedVersion)
        {
            return new RenameTagOutcome.VersionConflict(row.CurrentVersion);
        }

        var tag = TagRowMapper.ToDomain(row);
        var changed = tag.Rename(rawName, now);
        if (!changed)
        {
            return new RenameTagOutcome.NoChange(tag);
        }

        var collision = await ctx.Set<TagRow>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == tag.Name && r.Id != wire, ct);
        if (collision is not null)
        {
            return new RenameTagOutcome.NameTaken(TagRowMapper.ToDomain(collision));
        }

        var newName = tag.Name;
        var newVersion = tag.CurrentVersion;
        var newUpdatedAt = tag.UpdatedAt;

        try
        {
            var affected = await ctx.Set<TagRow>()
                .Where(r => r.Id == wire && r.CurrentVersion == expectedVersion)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Name, newName)
                    .SetProperty(r => r.CurrentVersion, newVersion)
                    .SetProperty(r => r.UpdatedAt, newUpdatedAt), ct);
            if (affected == 0)
            {
                var fresh = await ctx.Set<TagRow>().AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == wire, ct);
                return fresh is null
                    ? new RenameTagOutcome.NotFound()
                    : new RenameTagOutcome.VersionConflict(fresh.CurrentVersion);
            }
        }
        catch (DbUpdateException ex) when (IsUniqueConflict(ex))
        {
            var raced = await ctx.Set<TagRow>().AsNoTracking()
                .FirstOrDefaultAsync(r => r.Name == tag.Name && r.Id != wire, ct);
            if (raced is not null)
            {
                return new RenameTagOutcome.NameTaken(TagRowMapper.ToDomain(raced));
            }
            throw;
        }

        return new RenameTagOutcome.Renamed(tag);
    }

    public async Task<SetTagDefaultRepositoriesOutcome> SetDefaultRepositoriesAsync(
        TagId id,
        int expectedVersion,
        IReadOnlyList<TagDefaultRepository> defaultRepositories,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(defaultRepositories);
        var ctx = RequireContext(nameof(SetDefaultRepositoriesAsync));
        var wire = id.Value;

        var row = await ctx.Set<TagRow>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == wire, ct);
        if (row is null)
        {
            return new SetTagDefaultRepositoriesOutcome.NotFound();
        }
        if (row.CurrentVersion != expectedVersion)
        {
            return new SetTagDefaultRepositoriesOutcome.VersionConflict(row.CurrentVersion);
        }

        var tag = TagRowMapper.ToDomain(row);
        var changed = tag.ReplaceDefaultRepositories(defaultRepositories, now);
        if (!changed)
        {
            return new SetTagDefaultRepositoriesOutcome.NoChange(tag);
        }

        var payload = tag.DefaultRepositories.Count == 0
            ? new List<TagDefaultRepositoryPayload>()
            : [.. tag.DefaultRepositories.Select(TagRowMapper.ToPayload)];
        var newVersion = tag.CurrentVersion;
        var newUpdatedAt = tag.UpdatedAt;

        var affected = await ctx.Set<TagRow>()
            .Where(r => r.Id == wire && r.CurrentVersion == expectedVersion)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.DefaultRepositories, payload)
                .SetProperty(r => r.CurrentVersion, newVersion)
                .SetProperty(r => r.UpdatedAt, newUpdatedAt), ct);
        if (affected == 0)
        {
            var fresh = await ctx.Set<TagRow>().AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == wire, ct);
            return fresh is null
                ? new SetTagDefaultRepositoriesOutcome.NotFound()
                : new SetTagDefaultRepositoriesOutcome.VersionConflict(fresh.CurrentVersion);
        }

        return new SetTagDefaultRepositoriesOutcome.Updated(tag);
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
