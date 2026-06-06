using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Events;
using Throne.Domain.Intents.Linking;
using Throne.Domain.Tags;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoIntentLinkRepository(
    IMongoDatabase database,
    MongoSessionAccessor sessions,
    IIntentEventRepository intentEvents,
    TimeProvider clock) : IIntentLinkRepository
{
    private readonly IMongoCollection<IntentLinkDocument> _links =
        database.GetCollection<IntentLinkDocument>(MongoCollectionNames.IntentLinks);

    private readonly IMongoCollection<IntentDocument> _intents =
        database.GetCollection<IntentDocument>(MongoCollectionNames.Intents);

    public async Task<CreateIntentLinkOutcome> CreateAsync(IntentLink link, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(link);

        var session = RequireSession(nameof(CreateAsync));

        var missing = await FindMissingEndpointAsync(session, link.FromId, link.ToId, ct);
        if (missing is not null)
        {
            return new CreateIntentLinkOutcome.IntentNotFound(missing);
        }

        var existing = await FindEdgeAsync(session, link.FromId, link.ToId, link.Type, ct);
        if (existing is not null)
        {
            return new CreateIntentLinkOutcome.Duplicate(MapToDomain(existing));
        }

        await _links.InsertOneAsync(session, MapToDocument(link), options: null, ct);
        await intentEvents.AppendAsync(
            IntentEvent.ForLinkAdded(Guid.NewGuid().ToString("N"), link),
            ct);
        return new CreateIntentLinkOutcome.Created(link);
    }

    public async Task<DeleteIntentLinkOutcome> DeleteAsync(
        IntentId fromId,
        IntentId toId,
        string type,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        var session = RequireSession(nameof(DeleteAsync));

        var existing = await FindEdgeAsync(session, fromId, toId, type, ct);
        if (existing is null)
        {
            return new DeleteIntentLinkOutcome.NotFound();
        }

        await _links.DeleteOneAsync(
            session,
            Builders<IntentLinkDocument>.Filter.Eq(d => d.Id, existing.Id),
            options: null,
            ct);

        var domain = MapToDomain(existing);
        await intentEvents.AppendAsync(
            IntentEvent.ForLinkRemoved(Guid.NewGuid().ToString("N"), domain, clock.GetUtcNow()),
            ct);
        return new DeleteIntentLinkOutcome.Deleted(domain);
    }

    public async Task<IReadOnlyList<IntentLinkView>> ListByIntentAsync(IntentId intentId, CancellationToken ct)
    {
        var session = sessions.Current;
        var fb = Builders<IntentLinkDocument>.Filter;
        var filter = fb.Or(fb.Eq(d => d.FromId, intentId.Value), fb.Eq(d => d.ToId, intentId.Value));
        var find = session is null ? _links.Find(filter) : _links.Find(session, filter);
        var docs = await find.SortBy(d => d.CreatedAt).ToListAsync(ct);
        return await ProjectAsync(session, intentId, docs, ct);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<IntentLinkView>>> ListByIntentsAsync(
        IReadOnlyList<IntentId> intentIds,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(intentIds);

        var ids = intentIds.Select(i => i.Value).Distinct(StringComparer.Ordinal).ToList();
        var result = new Dictionary<string, IReadOnlyList<IntentLinkView>>(StringComparer.Ordinal);
        if (ids.Count == 0)
        {
            return result;
        }

        var session = sessions.Current;
        var fb = Builders<IntentLinkDocument>.Filter;
        var filter = fb.Or(fb.In(d => d.FromId, ids), fb.In(d => d.ToId, ids));
        var find = session is null ? _links.Find(filter) : _links.Find(session, filter);
        var docs = await find.SortBy(d => d.CreatedAt).ToListAsync(ct);

        var peerIds = new HashSet<string>(StringComparer.Ordinal);
        var queriedSet = ids.ToHashSet(StringComparer.Ordinal);
        foreach (var doc in docs)
        {
            // Edges whose peer was deleted drop out of the projection automatically:
            // LoadPeersAsync only returns intents that still exist.
            if (!queriedSet.Contains(doc.FromId))
            {
                peerIds.Add(doc.FromId);
            }
            if (!queriedSet.Contains(doc.ToId))
            {
                peerIds.Add(doc.ToId);
            }
        }
        // Queried intents themselves can be a peer when both endpoints of an edge
        // are in the query set, so include them in the load.
        foreach (var id in ids)
        {
            peerIds.Add(id);
        }

        var peersById = await LoadPeersAsync(session, peerIds, ct);

        var grouped = new Dictionary<string, List<IntentLinkView>>(StringComparer.Ordinal);
        foreach (var doc in docs)
        {
            var fromExists = peersById.ContainsKey(doc.FromId);
            var toExists = peersById.ContainsKey(doc.ToId);
            if (!fromExists || !toExists)
            {
                continue;
            }

            if (queriedSet.Contains(doc.FromId))
            {
                AppendView(grouped, doc.FromId, MapToDomain(doc), IntentLinkDirection.Outgoing, MapIntentToDomain(peersById[doc.ToId]));
            }
            if (queriedSet.Contains(doc.ToId))
            {
                AppendView(grouped, doc.ToId, MapToDomain(doc), IntentLinkDirection.Incoming, MapIntentToDomain(peersById[doc.FromId]));
            }
        }

        foreach (var (id, list) in grouped)
        {
            result[id] = list;
        }
        return result;
    }

    private static void AppendView(
        Dictionary<string, List<IntentLinkView>> bucket,
        string ownerId,
        IntentLink link,
        IntentLinkDirection direction,
        Intent peer)
    {
        if (!bucket.TryGetValue(ownerId, out var list))
        {
            list = [];
            bucket[ownerId] = list;
        }
        list.Add(new IntentLinkView(link, direction, peer));
    }

    public async Task<IntentLinksPage> ListPagedAsync(
        IntentId intentId,
        IntentLinkDirection? direction,
        string? type,
        int limit,
        string? cursor,
        CancellationToken ct)
    {
        var session = sessions.Current;
        var filter = BuildPageFilter(intentId, direction, type, cursor);
        var sort = Builders<IntentLinkDocument>.Sort
            .Combine(
                Builders<IntentLinkDocument>.Sort.Ascending(d => d.CreatedAt),
                Builders<IntentLinkDocument>.Sort.Ascending(d => d.Id));

        var pageSize = limit + 1;
        var find = session is null
            ? _links.Find(filter).Sort(sort).Limit(pageSize)
            : _links.Find(session, filter).Sort(sort).Limit(pageSize);
        var docs = await find.ToListAsync(ct);

        var hasMore = docs.Count > limit;
        var pageDocs = hasMore ? docs.Take(limit).ToList() : docs;
        var items = await ProjectAsync(session, intentId, pageDocs, ct);

        string? next = null;
        if (hasMore && pageDocs.Count > 0)
        {
            var last = pageDocs[^1];
            next = IntentLinkCursor.Encode(last.CreatedAt, last.Id);
        }

        return new IntentLinksPage(items, next);
    }

    public async Task<IReadOnlyDictionary<string, int>> CountIncomingByTypeAsync(
        IReadOnlyList<IntentId> intentIds,
        string type,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        if (intentIds.Count == 0)
        {
            return result;
        }

        var ids = intentIds.Select(i => i.Value).Distinct(StringComparer.Ordinal).ToList();

        var session = sessions.Current;
        var fb = Builders<IntentLinkDocument>.Filter;
        var filter = fb.And(
            fb.Eq(d => d.Type, type),
            fb.In(d => d.ToId, ids));
        var find = session is null ? _links.Find(filter) : _links.Find(session, filter);
        var docs = await find.Project(d => new { d.FromId, d.ToId }).ToListAsync(ct);

        foreach (var doc in docs)
        {
            result.TryGetValue(doc.ToId, out var current);
            result[doc.ToId] = current + 1;
        }
        return result;
    }

    private static FilterDefinition<IntentLinkDocument> BuildPageFilter(
        IntentId intentId,
        IntentLinkDirection? direction,
        string? type,
        string? cursor)
    {
        var fb = Builders<IntentLinkDocument>.Filter;
        var clauses = new List<FilterDefinition<IntentLinkDocument>>
        {
            direction switch
            {
                IntentLinkDirection.Outgoing => fb.Eq(d => d.FromId, intentId.Value),
                IntentLinkDirection.Incoming => fb.Eq(d => d.ToId, intentId.Value),
                _ => fb.Or(fb.Eq(d => d.FromId, intentId.Value), fb.Eq(d => d.ToId, intentId.Value)),
            },
        };
        if (!string.IsNullOrWhiteSpace(type))
        {
            clauses.Add(fb.Eq(d => d.Type, type));
        }
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            var (cursorTime, cursorId) = IntentLinkCursor.Decode(cursor);
            clauses.Add(fb.Or(
                fb.Gt(d => d.CreatedAt, cursorTime),
                fb.And(fb.Eq(d => d.CreatedAt, cursorTime), fb.Gt(d => d.Id, cursorId))));
        }
        return fb.And(clauses);
    }

    private async Task<List<IntentLinkView>> ProjectAsync(
        IClientSessionHandle? session,
        IntentId intentId,
        List<IntentLinkDocument> docs,
        CancellationToken ct)
    {
        if (docs.Count == 0)
        {
            return [];
        }

        var peerIds = CollectPeerIds(intentId, docs);
        var peersById = await LoadPeersAsync(session, peerIds, ct);

        var result = new List<IntentLinkView>(docs.Count);
        foreach (var doc in docs)
        {
            var direction = string.Equals(doc.FromId, intentId.Value, StringComparison.Ordinal)
                ? IntentLinkDirection.Outgoing
                : IntentLinkDirection.Incoming;
            var peerId = direction == IntentLinkDirection.Outgoing ? doc.ToId : doc.FromId;
            if (!peersById.TryGetValue(peerId, out var peer))
            {
                continue;
            }
            result.Add(new IntentLinkView(MapToDomain(doc), direction, MapIntentToDomain(peer)));
        }
        return result;
    }

    private static HashSet<string> CollectPeerIds(IntentId intentId, IReadOnlyList<IntentLinkDocument> docs)
    {
        var peerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var doc in docs)
        {
            peerIds.Add(string.Equals(doc.FromId, intentId.Value, StringComparison.Ordinal) ? doc.ToId : doc.FromId);
        }
        return peerIds;
    }

    private async Task<Dictionary<string, IntentDocument>> LoadPeersAsync(
        IClientSessionHandle? session,
        HashSet<string> peerIds,
        CancellationToken ct)
    {
        var peerFilter = Builders<IntentDocument>.Filter.In(d => d.Id, peerIds);
        var find = session is null ? _intents.Find(peerFilter) : _intents.Find(session, peerFilter);
        var peers = await find.ToListAsync(ct);
        return peers.ToDictionary(p => p.Id, p => p, StringComparer.Ordinal);
    }

    private async Task<string?> FindMissingEndpointAsync(
        IClientSessionHandle session,
        IntentId fromId,
        IntentId toId,
        CancellationToken ct)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal) { fromId.Value, toId.Value };
        var filter = Builders<IntentDocument>.Filter.In(d => d.Id, ids);
        var found = (await _intents.Find(session, filter).Project(d => d.Id).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        if (!found.Contains(fromId.Value))
        {
            return fromId.Value;
        }
        if (!found.Contains(toId.Value))
        {
            return toId.Value;
        }
        return null;
    }

    private async Task<IntentLinkDocument?> FindEdgeAsync(
        IClientSessionHandle session,
        IntentId fromId,
        IntentId toId,
        string type,
        CancellationToken ct)
    {
        var filter = Builders<IntentLinkDocument>.Filter.And(
            Builders<IntentLinkDocument>.Filter.Eq(d => d.FromId, fromId.Value),
            Builders<IntentLinkDocument>.Filter.Eq(d => d.ToId, toId.Value),
            Builders<IntentLinkDocument>.Filter.Eq(d => d.Type, type));
        return await _links.Find(session, filter).FirstOrDefaultAsync(ct);
    }

    private IClientSessionHandle RequireSession(string method) =>
        sessions.Current
            ?? throw new InvalidOperationException(
                $"MongoIntentLinkRepository.{method} must run inside IUnitOfWork.ExecuteAsync.");

    private static IntentLinkDocument MapToDocument(IntentLink link) => new()
    {
        Id = link.Id,
        FromId = link.FromId.Value,
        ToId = link.ToId.Value,
        Type = link.Type,
        Author = link.Author.ToWire(),
        Rationale = link.Rationale,
        CreatedAt = link.CreatedAt.UtcDateTime,
    };

    private static IntentLink MapToDomain(IntentLinkDocument doc) => new(
        Id: doc.Id,
        FromId: new IntentId(doc.FromId),
        ToId: new IntentId(doc.ToId),
        Type: doc.Type,
        Author: IntentLinkAuthorExtensions.FromWire(doc.Author),
        Rationale: doc.Rationale,
        CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc)));

    private static Intent MapIntentToDomain(IntentDocument doc) => Intent.Restore(
        id: new IntentId(doc.Id),
        text: doc.Text,
        status: string.IsNullOrWhiteSpace(doc.Status) ? IntentStatusNames.Draft : doc.Status,
        currentVersion: doc.CurrentVersion,
        tagIds: doc.TagIds.Select(v => new TagId(v)).ToList(),
        sortKey: doc.SortKey,
        createdAt: DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc),
        updatedAt: DateTime.SpecifyKind(doc.UpdatedAt, DateTimeKind.Utc));
}
