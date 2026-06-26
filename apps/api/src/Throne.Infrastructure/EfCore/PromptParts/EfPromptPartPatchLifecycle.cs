using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Application.PromptPartPatches;
using Throne.Domain.PromptParts;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.PromptParts;

/// <summary>
/// Reads + create writes for prompt-part patches. Insert is idempotency-aware: when an
/// <c>idempotency_key</c> is supplied, a UNIQUE-conflict on the partial index resolves the
/// race by returning the original winner with <c>IsExisting=true</c> so the proposed event
/// does not fire twice.
/// </summary>
internal sealed class EfPromptPartPatchLifecycle(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions)
{
    private const int SqliteConstraintErrorCode = 19;
    private const int SqliteUniqueErrorCode = 2067;

    public async Task<CreatePromptPartPatchOutcome> CreateAsync(
        PromptPartPatch patch,
        string? idempotencyKey,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var ctx = RequireContext(nameof(CreateAsync));

        ctx.Set<PromptPartPatchRow>().Add(PromptPartPatchRowMapper.ToRow(patch, idempotencyKey));
        try
        {
            await ctx.SaveChangesAsync(ct);
            return new CreatePromptPartPatchOutcome(patch);
        }
        catch (DbUpdateException ex) when (idempotencyKey is not null && IsUniqueConflict(ex))
        {
            DetachInserts(ctx);
            var existing = await ctx.Set<PromptPartPatchRow>().AsNoTracking()
                .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, ct);
            if (existing is null)
            {
                throw;
            }
            return new CreatePromptPartPatchOutcome(PromptPartPatchRowMapper.ToDomain(existing), IsExisting: true);
        }
    }

    public Task<PromptPartPatch?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        return ReadAsync(async (ctx, c) =>
        {
            var row = await ctx.Set<PromptPartPatchRow>().AsNoTracking()
                .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, c);
            return row is null ? null : PromptPartPatchRowMapper.ToDomain(row);
        }, ct);
    }

    public Task<PromptPartPatch?> GetAsync(string id, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return ReadAsync(async (ctx, c) =>
        {
            var row = await ctx.Set<PromptPartPatchRow>().AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id, c);
            return row is null ? null : PromptPartPatchRowMapper.ToDomain(row);
        }, ct);
    }

    public Task<PromptPartPatchPage> ListAsync(
        PromptPartPatchListFilter filter,
        int limit,
        string? cursor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (limit < 1)
        {
            limit = 1;
        }
        return ReadAsync(async (ctx, c) =>
        {
            var query = ctx.Set<PromptPartPatchRow>().AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                var status = filter.Status;
                query = query.Where(r => r.Status == status);
            }
            if (!string.IsNullOrWhiteSpace(filter.TargetScope))
            {
                var scope = filter.TargetScope;
                query = query.Where(r => r.TargetScope == scope);
            }
            if (!string.IsNullOrWhiteSpace(filter.TargetKey))
            {
                var key = filter.TargetKey;
                query = query.Where(r => r.TargetKey == key);
            }
            if (cursor is not null && PromptPartPatchEfCursor.TryDecode(cursor, out var decoded))
            {
                var pivotCreatedAt = decoded.CreatedAt;
                var pivotId = decoded.Id;
                query = query.Where(r =>
                    r.CreatedAt < pivotCreatedAt
                    || (r.CreatedAt == pivotCreatedAt
                        && r.Id.CompareTo(pivotId) < 0));
            }

            var rows = await query
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id)
                .Take(limit + 1)
                .ToListAsync(c);

            string? nextCursor = null;
            if (rows.Count > limit)
            {
                var pivot = rows[limit - 1];
                nextCursor = PromptPartPatchEfCursor.Encode(pivot.Id, pivot.CreatedAt);
                rows.RemoveRange(limit, rows.Count - limit);
            }

            var items = rows.Select(PromptPartPatchRowMapper.ToDomain).ToList();
            return new PromptPartPatchPage(items, nextCursor);
        }, ct);
    }

    private ThroneDbContext RequireContext(string method) =>
        Sessions.Current
            ?? throw new InvalidOperationException(
                $"EfPromptPartPatchLifecycle.{method} must run inside IUnitOfWork.ExecuteAsync.");

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
        foreach (var entry in ctx.ChangeTracker.Entries<PromptPartPatchRow>().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}

/// <summary>
/// Opaque list cursor: base64 of (created_at ticks, id). Persistent cursor uses the
/// <c>PromptPartPatchCursor</c> shape.
/// </summary>
internal static class PromptPartPatchEfCursor
{
    public static string Encode(string id, DateTimeOffset createdAt)
    {
        var payload = $"{createdAt.UtcTicks.ToString(CultureInfo.InvariantCulture)}|{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    public static bool TryDecode(string cursor, out PromptPartPatchCursorValue value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }
        try
        {
            var bytes = Convert.FromBase64String(cursor);
            var payload = Encoding.UTF8.GetString(bytes);
            var parts = payload.Split('|', 2);
            if (parts.Length != 2)
            {
                return false;
            }
            if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
            {
                return false;
            }
            value = new PromptPartPatchCursorValue(parts[1], new DateTimeOffset(ticks, TimeSpan.Zero));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

internal readonly record struct PromptPartPatchCursorValue(string Id, DateTimeOffset CreatedAt);
