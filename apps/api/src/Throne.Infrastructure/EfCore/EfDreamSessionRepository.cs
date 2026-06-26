using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Throne.Application.Dreams;
using Throne.Application.Ports;
using Throne.Domain.Dreams;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// EF Core persistence for <see cref="DreamSession"/>. Sessions are append-only —
/// no update / delete methods. List pagination is descending by <c>created_at</c>
/// with <c>id</c> tiebreaker; cursor format matches the Mongo backend so cross-backend
/// dumps and HTTP integrations are byte-stable.
/// </summary>
internal sealed class EfDreamSessionRepository(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions), IDreamSessionRepository
{
    public async Task<CreateDreamSessionOutcome> CreateAsync(DreamSession session, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        var ctx = RequireWriteContext(nameof(CreateAsync));
        ctx.Set<DreamSessionRow>().Add(DreamSessionRowMapper.ToRow(session));
        await ctx.SaveChangesAsync(ct);
        return new CreateDreamSessionOutcome(session);
    }

    public Task<DreamSession?> GetAsync(string id, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return ReadAsync(async (ctx, c) =>
        {
            var row = await ctx.Set<DreamSessionRow>().AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id, c);
            return row is null ? null : DreamSessionRowMapper.ToDomain(row);
        }, ct);
    }

    public Task<DreamSessionPage> ListAsync(
        DreamSessionListFilter filter,
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
            var query = ctx.Set<DreamSessionRow>().AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.Vendor))
            {
                var vendor = filter.Vendor;
                query = query.Where(r => r.Vendor == vendor);
            }
            if (!string.IsNullOrWhiteSpace(filter.Host))
            {
                var host = filter.Host;
                query = query.Where(r => r.Host == host);
            }
            if (cursor is not null && DreamSessionCursorCodec.TryDecode(cursor, out var decoded))
            {
                var cutCreatedAt = decoded.CreatedAt;
                var cutId = decoded.Id;
                query = query.Where(r =>
                    r.CreatedAt < cutCreatedAt
                    || (r.CreatedAt == cutCreatedAt && string.Compare(r.Id, cutId, StringComparison.Ordinal) < 0));
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
                nextCursor = DreamSessionCursorCodec.Encode(pivot.Id, pivot.CreatedAt);
                rows.RemoveRange(limit, rows.Count - limit);
            }

            var items = rows.Select(DreamSessionRowMapper.ToDomain).ToList();
            return new DreamSessionPage(items, nextCursor);
        }, ct);
    }

    private ThroneDbContext RequireWriteContext(string method) =>
        Sessions.Current
            ?? throw new InvalidOperationException(
                $"EfDreamSessionRepository.{method} must run inside IUnitOfWork.ExecuteAsync.");
}

/// <summary>
/// Opaque base64 cursor (created_at_ticks|id). Wire format matches the Mongo dream
/// session cursor so clients can pass cursors between backends without recoding.
/// </summary>
internal static class DreamSessionCursorCodec
{
    public static string Encode(string id, DateTimeOffset createdAt)
    {
        var payload = $"{createdAt.UtcTicks.ToString(CultureInfo.InvariantCulture)}|{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    public static bool TryDecode(string cursor, out DreamSessionCursorPoint point)
    {
        point = default;
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
            point = new DreamSessionCursorPoint(parts[1], new DateTimeOffset(ticks, TimeSpan.Zero));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

internal readonly record struct DreamSessionCursorPoint(string Id, DateTimeOffset CreatedAt);
