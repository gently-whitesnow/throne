using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Events;
using Throne.Domain.Intents.Linking;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoIntentLinkRepository
    : MongoRepositoryBase<IntentLinkDocument, string>, IIntentLinkRepository
{
    // Secondary `intents` collection is used purely to validate endpoints / project
    // peer aggregates — it's not part of the link aggregate identity, so it stays
    // off the base helper.
    private readonly IMongoCollection<IntentDocument> _intents;
    private readonly IIntentEventRepository _intentEvents;
    private readonly TimeProvider _clock;

    public MongoIntentLinkRepository(
        IMongoDatabase database,
        MongoSessionAccessor sessions,
        IIntentEventRepository intentEvents,
        TimeProvider clock)
        : base(database, MongoCollectionNames.IntentLinks, sessions)
    {
        _intents = database.GetCollection<IntentDocument>(MongoCollectionNames.Intents);
        _intentEvents = intentEvents;
        _clock = clock;
    }

    protected override FilterDefinition<IntentLinkDocument> ById(string id) =>
        Builders<IntentLinkDocument>.Filter.Eq(d => d.Id, id);

    public async Task<CreateIntentLinkOutcome> CreateAsync(IntentLink link, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(link);

        var session = RequireSession(nameof(CreateAsync));

        var missing = await FindMissingEndpointAsync(session, link.FromId, link.ToId, ct);
        if (missing is not null)
        {
            return new CreateIntentLinkOutcome.IntentNotFound(missing);
        }

        var existing = await FindEdgeAsync(session, link.FromId, link.ToId, ct);
        if (existing is not null)
        {
            return new CreateIntentLinkOutcome.Duplicate(MongoIntentLinkMapper.ToDomain(existing));
        }

        await InsertOneAsync(MongoIntentLinkMapper.ToDocument(link), ct);
        await _intentEvents.AppendAsync(
            IntentEvent.ForLinkAdded(Guid.NewGuid().ToString("N"), link),
            ct);
        return new CreateIntentLinkOutcome.Created(link);
    }

    public async Task<DeleteIntentLinkOutcome> DeleteAsync(
        IntentId fromId,
        IntentId toId,
        CancellationToken ct)
    {
        var session = RequireSession(nameof(DeleteAsync));

        var existing = await FindEdgeAsync(session, fromId, toId, ct);
        if (existing is null)
        {
            return new DeleteIntentLinkOutcome.NotFound();
        }

        await DeleteOneAsync(ById(existing.Id), ct);

        var domain = MongoIntentLinkMapper.ToDomain(existing);
        await _intentEvents.AppendAsync(
            IntentEvent.ForLinkRemoved(Guid.NewGuid().ToString("N"), domain, _clock.GetUtcNow()),
            ct);
        return new DeleteIntentLinkOutcome.Deleted(domain);
    }

    public async Task<IReadOnlyList<IntentLinkView>> ListByIntentAsync(IntentId intentId, CancellationToken ct)
    {
        var fb = Builders<IntentLinkDocument>.Filter;
        var filter = fb.Or(fb.Eq(d => d.FromId, intentId.Value), fb.Eq(d => d.ToId, intentId.Value));
        var docs = await Find(filter).SortBy(d => d.CreatedAt).ToListAsync(ct);
        return await MongoIntentLinkProjection.ProjectAsync(_intents, Sessions.Current, intentId, docs, ct);
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

        var fb = Builders<IntentLinkDocument>.Filter;
        var filter = fb.Or(fb.In(d => d.FromId, ids), fb.In(d => d.ToId, ids));
        var docs = await Find(filter).SortBy(d => d.CreatedAt).ToListAsync(ct);
        var session = Sessions.Current;

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

        var peersById = await MongoIntentLinkProjection.LoadPeersAsync(_intents, session, peerIds, ct);

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
                AppendView(grouped, doc.FromId, MongoIntentLinkMapper.ToDomain(doc), IntentLinkDirection.Outgoing, MongoIntentLinkMapper.IntentToDomain(peersById[doc.ToId]));
            }
            if (queriedSet.Contains(doc.ToId))
            {
                AppendView(grouped, doc.ToId, MongoIntentLinkMapper.ToDomain(doc), IntentLinkDirection.Incoming, MongoIntentLinkMapper.IntentToDomain(peersById[doc.FromId]));
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
        bool? blocking,
        int limit,
        string? cursor,
        CancellationToken ct)
    {
        var filter = BuildPageFilter(intentId, direction, blocking, cursor);
        var sort = Builders<IntentLinkDocument>.Sort
            .Combine(
                Builders<IntentLinkDocument>.Sort.Ascending(d => d.CreatedAt),
                Builders<IntentLinkDocument>.Sort.Ascending(d => d.Id));

        var pageSize = limit + 1;
        var docs = await Find(filter).Sort(sort).Limit(pageSize).ToListAsync(ct);

        var hasMore = docs.Count > limit;
        var pageDocs = hasMore ? docs.Take(limit).ToList() : docs;
        var items = await MongoIntentLinkProjection.ProjectAsync(_intents, Sessions.Current, intentId, pageDocs, ct);

        string? next = null;
        if (hasMore && pageDocs.Count > 0)
        {
            var last = pageDocs[^1];
            next = IntentLinkCursor.Encode(last.CreatedAt, last.Id);
        }

        return new IntentLinksPage(items, next);
    }

    private static FilterDefinition<IntentLinkDocument> BuildPageFilter(
        IntentId intentId,
        IntentLinkDirection? direction,
        bool? blocking,
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
        if (blocking is not null)
        {
            clauses.Add(fb.Eq(d => d.Blocking, blocking.Value));
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
        CancellationToken ct)
    {
        var filter = Builders<IntentLinkDocument>.Filter.And(
            Builders<IntentLinkDocument>.Filter.Eq(d => d.FromId, fromId.Value),
            Builders<IntentLinkDocument>.Filter.Eq(d => d.ToId, toId.Value));
        return await Find(filter).FirstOrDefaultAsync(ct);
    }

    private IClientSessionHandle RequireSession(string method) =>
        Sessions.Current
            ?? throw new InvalidOperationException(
                $"MongoIntentLinkRepository.{method} must run inside IUnitOfWork.ExecuteAsync.");

}
