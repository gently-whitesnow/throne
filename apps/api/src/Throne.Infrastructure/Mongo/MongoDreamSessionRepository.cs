using System.Globalization;
using System.Text;
using MongoDB.Driver;
using Throne.Application.Auth;
using Throne.Application.Dreams;
using Throne.Application.Ports;
using Throne.Domain.Dreams;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Mongo persistence for <see cref="DreamSession"/>. Owner filter is applied
/// on every read; <c>MongoOwnerUserIdIsolationTests</c> guards the invariant.
/// Sessions are append-only — no update / delete methods.
/// </summary>
internal sealed class MongoDreamSessionRepository(
    IMongoDatabase database,
    MongoSessionAccessor sessions,
    ICurrentUserAccessor currentUser) : IDreamSessionRepository
{
    private readonly IMongoCollection<DreamSessionDocument> _collection =
        database.GetCollection<DreamSessionDocument>(MongoCollectionNames.DreamSessions);

    public async Task<CreateDreamSessionOutcome> CreateAsync(DreamSession session, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        var dbSession = RequireSession(nameof(CreateAsync));
        var doc = ToDocument(session);
        await _collection.InsertOneAsync(dbSession, doc, options: null, ct);
        return new CreateDreamSessionOutcome(session);
    }

    public async Task<DreamSession?> GetAsync(string id, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var filter = Builders<DreamSessionDocument>.Filter.And(
            Builders<DreamSessionDocument>.Filter.Eq(x => x.Id, id),
            Builders<DreamSessionDocument>.Filter.Eq(x => x.OwnerUserId, currentUser.UserId));
        var dbSession = sessions.Current;
        var doc = dbSession is null
            ? await _collection.Find(filter).FirstOrDefaultAsync(ct)
            : await _collection.Find(dbSession, filter).FirstOrDefaultAsync(ct);
        return doc is null ? null : ToDomain(doc);
    }

    public async Task<DreamSessionPage> ListAsync(
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

        var clauses = new List<FilterDefinition<DreamSessionDocument>>
        {
            Builders<DreamSessionDocument>.Filter.Eq(x => x.OwnerUserId, currentUser.UserId),
        };
        if (!string.IsNullOrWhiteSpace(filter.Vendor))
        {
            clauses.Add(Builders<DreamSessionDocument>.Filter.Eq(x => x.Vendor, filter.Vendor));
        }
        if (cursor is not null && DreamSessionCursor.TryDecode(cursor, out var decoded))
        {
            clauses.Add(Builders<DreamSessionDocument>.Filter.Or(
                Builders<DreamSessionDocument>.Filter.Lt(x => x.CreatedAt, decoded.CreatedAt),
                Builders<DreamSessionDocument>.Filter.And(
                    Builders<DreamSessionDocument>.Filter.Eq(x => x.CreatedAt, decoded.CreatedAt),
                    Builders<DreamSessionDocument>.Filter.Lt(x => x.Id, decoded.Id))));
        }

        var docs = await _collection
            .Find(Builders<DreamSessionDocument>.Filter.And(clauses))
            .Sort(Builders<DreamSessionDocument>.Sort
                .Descending(x => x.CreatedAt)
                .Descending(x => x.Id))
            .Limit(limit + 1)
            .ToListAsync(ct);

        string? nextCursor = null;
        if (docs.Count > limit)
        {
            var pivot = docs[limit - 1];
            nextCursor = DreamSessionCursor.Encode(pivot.Id, pivot.CreatedAt);
            docs.RemoveRange(limit, docs.Count - limit);
        }

        var items = docs.Select(ToDomain).ToList();
        return new DreamSessionPage(items, nextCursor);
    }

    private IClientSessionHandle RequireSession(string method) =>
        sessions.Current
            ?? throw new InvalidOperationException(
                $"MongoDreamSessionRepository.{method} must run inside IUnitOfWork.ExecuteAsync.");

    private static DreamSessionDocument ToDocument(DreamSession session) =>
        DreamSessionMongoMapper.ToDocument(session);

    private static DreamSession ToDomain(DreamSessionDocument doc) =>
        DreamSessionMongoMapper.ToDomain(doc);
}

internal static class DreamSessionMongoMapper
{
    public static DreamSessionDocument ToDocument(DreamSession session) => new()
    {
        Id = session.Id,
        OwnerUserId = session.OwnerUserId,
        CreatedAt = session.Identity.CreatedAt.UtcDateTime,
        Vendor = session.Payload.Vendor,
        DateFrom = session.Payload.DateFrom?.UtcDateTime,
        DateTo = session.Payload.DateTo?.UtcDateTime,
        ProcessedConversationIds = session.Payload.ProcessedConversationIds.ToList(),
        Summary = session.Payload.Summary,
        Reflection = session.Payload.Reflection,
        ProposedPatchIds = session.Payload.ProposedPatchIds.ToList(),
    };

    public static DreamSession ToDomain(DreamSessionDocument doc) => DreamSession.Restore(
        id: doc.Id,
        ownerUserId: string.IsNullOrWhiteSpace(doc.OwnerUserId) ? CurrentUserIds.LocalDev : doc.OwnerUserId,
        createdAt: ToUtcOffset(doc.CreatedAt),
        vendor: doc.Vendor,
        dateFrom: doc.DateFrom is { } from ? ToUtcOffset(from) : null,
        dateTo: doc.DateTo is { } to ? ToUtcOffset(to) : null,
        processedConversationIds: doc.ProcessedConversationIds ?? new List<string>(),
        summary: doc.Summary ?? string.Empty,
        reflection: doc.Reflection,
        proposedPatchIds: doc.ProposedPatchIds ?? new List<string>());

    private static DateTimeOffset ToUtcOffset(DateTime utc) =>
        new(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeSpan.Zero);
}

/// <summary>
/// Opaque list cursor: base64 of (created_at, id). Mirrors the shape used by
/// other paginated repositories so callers can treat cursors uniformly.
/// </summary>
internal static class DreamSessionCursor
{
    public static string Encode(string id, DateTime createdAtUtc)
    {
        var payload = $"{createdAtUtc.Ticks.ToString(CultureInfo.InvariantCulture)}|{id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    public static bool TryDecode(string cursor, out DreamSessionCursorValue value)
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
            value = new DreamSessionCursorValue(parts[1], new DateTime(ticks, DateTimeKind.Utc));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

internal readonly record struct DreamSessionCursorValue(string Id, DateTime CreatedAt);
