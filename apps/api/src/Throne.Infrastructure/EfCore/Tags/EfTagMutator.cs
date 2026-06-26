using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Tags;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Infrastructure.EfCore.Tags;

/// <summary>
/// Tag mutation surface (Rename / SetDefaultRepositories). Lifted out of
/// <see cref="EfTagLifecycle"/> so neither type exceeds the per-type LOC budget; both
/// classes share the same CAS pattern but disjoint columns.
/// </summary>
internal sealed class EfTagMutator(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions)
{
    private const int SqliteConstraintErrorCode = 19;
    private const int SqliteUniqueErrorCode = 2067;

    public async Task<RenameTagOutcome> RenameAsync(
        TagId id,
        int expectedVersion,
        string rawName,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var ctx = RequireContext(nameof(RenameAsync));
        var wire = id.Value;

        var preload = await LoadForCasAsync(ctx, wire, expectedVersion, ct);
        if (preload.NotFound)
        {
            return new RenameTagOutcome.NotFound();
        }
        if (preload.ConflictVersion is { } conflict)
        {
            return new RenameTagOutcome.VersionConflict(conflict);
        }

        var tag = TagRowMapper.ToDomain(preload.Row!);
        if (!tag.Rename(rawName, now))
        {
            return new RenameTagOutcome.NoChange(tag);
        }

        var collisionFound = await FindNameCollisionAsync(ctx, tag.Name, wire, ct);
        if (collisionFound is not null)
        {
            return new RenameTagOutcome.NameTaken(collisionFound);
        }

        return await ExecuteRenameAsync(ctx, tag, wire, expectedVersion, ct);
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

        var preload = await LoadForCasAsync(ctx, wire, expectedVersion, ct);
        if (preload.NotFound)
        {
            return new SetTagDefaultRepositoriesOutcome.NotFound();
        }
        if (preload.ConflictVersion is { } conflict)
        {
            return new SetTagDefaultRepositoriesOutcome.VersionConflict(conflict);
        }

        var tag = TagRowMapper.ToDomain(preload.Row!);
        if (!tag.ReplaceDefaultRepositories(defaultRepositories, now))
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

    private static async Task<(TagRow? Row, bool NotFound, int? ConflictVersion)> LoadForCasAsync(
        ThroneDbContext ctx, string wire, int expectedVersion, CancellationToken ct)
    {
        var row = await ctx.Set<TagRow>().AsNoTracking().FirstOrDefaultAsync(r => r.Id == wire, ct);
        if (row is null)
        {
            return (null, true, null);
        }
        return row.CurrentVersion != expectedVersion
            ? (row, false, row.CurrentVersion)
            : (row, false, null);
    }

    private static async Task<Tag?> FindNameCollisionAsync(
        ThroneDbContext ctx, string name, string excludeId, CancellationToken ct)
    {
        var collision = await ctx.Set<TagRow>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Name == name && r.Id != excludeId, ct);
        return collision is null ? null : TagRowMapper.ToDomain(collision);
    }

    private static async Task<RenameTagOutcome> ExecuteRenameAsync(
        ThroneDbContext ctx, Tag tag, string wire, int expectedVersion, CancellationToken ct)
    {
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
            var raced = await FindNameCollisionAsync(ctx, tag.Name, wire, ct);
            if (raced is not null)
            {
                return new RenameTagOutcome.NameTaken(raced);
            }
            throw;
        }
        return new RenameTagOutcome.Renamed(tag);
    }

    private ThroneDbContext RequireContext(string method) =>
        Sessions.Current
            ?? throw new InvalidOperationException(
                $"EfTagMutator.{method} must run inside IUnitOfWork.ExecuteAsync.");

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
}
