using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Repositories;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// EF Core registry for <see cref="Repository"/> rows (ADR-0031). Idempotent ensure:
/// existing row → <see cref="EnsureRepositoryOutcome.Existed"/> (no events), new row →
/// <see cref="EnsureRepositoryOutcome.Created"/>. The UNIQUE
/// <c>(provider, host, owner, repo)</c> index closes the first-appearance race; the
/// SQLite duplicate-key path re-reads the winner.
/// </summary>
internal sealed class EfRepositoryRegistry(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions), IRepositoryRegistry
{
    private const int SqliteConstraintErrorCode = 19;
    private const int SqliteUniqueErrorCode = 2067;

    public async Task<EnsureRepositoryOutcome> EnsureRepositoryAsync(
        RepoCoordinate coordinate, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(coordinate);

        var existing = await FindRowAsync(coordinate, ct);
        if (existing is not null)
        {
            return new EnsureRepositoryOutcome.Existed(RepositoryRowMapper.ToDomain(existing));
        }

        var ctx = RequireWriteContext(nameof(EnsureRepositoryAsync));
        var repository = Repository.Create(RepositoryId.New(), coordinate, now);
        ctx.Set<RepositoryRow>().Add(RepositoryRowMapper.ToRow(repository));
        try
        {
            await ctx.SaveChangesAsync(ct);
            return new EnsureRepositoryOutcome.Created(repository);
        }
        catch (DbUpdateException ex) when (IsUniqueConflict(ex))
        {
            DetachInserts(ctx);
            var raced = await FindRowAsync(coordinate, ct)
                ?? throw new InvalidOperationException(
                    $"Repository '{coordinate.Provider}/{coordinate.Owner}/{coordinate.Repo}' "
                    + "UNIQUE conflict but re-read returned null.", ex);
            return new EnsureRepositoryOutcome.Existed(RepositoryRowMapper.ToDomain(raced));
        }
    }

    public Task<Repository?> FindByCoordinateAsync(RepoCoordinate coordinate, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        return ReadAsync(async (ctx, c) =>
        {
            var row = await FindRowQuery(ctx, coordinate).FirstOrDefaultAsync(c);
            return row is null ? null : RepositoryRowMapper.ToDomain(row);
        }, ct);
    }

    public Task<IReadOnlyList<Repository>> ListAsync(CancellationToken ct) =>
        ReadAsync<IReadOnlyList<Repository>>(async (ctx, c) =>
        {
            var rows = await ctx.Set<RepositoryRow>().AsNoTracking()
                .OrderBy(r => r.Provider).ThenBy(r => r.Owner).ThenBy(r => r.Repo)
                .ToListAsync(c);
            return rows.Select(RepositoryRowMapper.ToDomain).ToList();
        }, ct);

    private Task<RepositoryRow?> FindRowAsync(RepoCoordinate coordinate, CancellationToken ct) =>
        ReadAsync(async (ctx, c) =>
            await FindRowQuery(ctx, coordinate).FirstOrDefaultAsync(c), ct);

    private static IQueryable<RepositoryRow> FindRowQuery(ThroneDbContext ctx, RepoCoordinate coordinate)
    {
        // Lookup mirrors the Mongo (provider, owner, repo) match: host is implied by the
        // domain normalization and is here only to scope GitLab self-hosted instances.
        var provider = coordinate.Provider;
        var owner = coordinate.Owner;
        var repo = coordinate.Repo;
        return ctx.Set<RepositoryRow>().AsNoTracking()
            .Where(r => r.Provider == provider && r.Owner == owner && r.Repo == repo);
    }

    private ThroneDbContext RequireWriteContext(string method) =>
        Sessions.Current
            ?? throw new InvalidOperationException(
                $"EfRepositoryRegistry.{method} must run inside IUnitOfWork.ExecuteAsync.");

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
        foreach (var entry in ctx.ChangeTracker.Entries<RepositoryRow>().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
