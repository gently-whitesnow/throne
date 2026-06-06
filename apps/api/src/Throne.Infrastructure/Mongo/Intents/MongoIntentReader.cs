using MongoDB.Driver;
using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Intents;

internal sealed class MongoIntentReader(
    IMongoDatabase database,
    MongoSessionAccessor sessions)
{
    private readonly IMongoCollection<IntentDocument> _intents =
        database.GetCollection<IntentDocument>(MongoCollectionNames.Intents);

    private readonly IMongoCollection<IntentPinDocument> _pins =
        database.GetCollection<IntentPinDocument>(MongoCollectionNames.IntentPins);

    public async Task<Intent?> GetByIdAsync(IntentId id, CancellationToken ct)
    {
        var session = sessions.Current;
        var byId = IntentCollectionFilters.ById(id.Value);
        var document = session is null
            ? await _intents.Find(byId).FirstOrDefaultAsync(ct)
            : await _intents.Find(session, byId).FirstOrDefaultAsync(ct);

        return document is null ? null : IntentDocumentMapper.ToDomain(document);
    }

    public async Task<Intent?> GetByIdForSystemAsync(IntentId id, CancellationToken ct)
    {
        var session = sessions.Current;
        var byId = Builders<IntentDocument>.Filter.Eq(d => d.Id, id.Value);
        var document = session is null
            ? await _intents.Find(byId).FirstOrDefaultAsync(ct)
            : await _intents.Find(session, byId).FirstOrDefaultAsync(ct);

        return document is null ? null : IntentDocumentMapper.ToDomain(document);
    }

    public async Task<IReadOnlyList<Intent>> ListAsync(IReadOnlyList<string>? statuses, CancellationToken ct)
    {
        var session = sessions.Current;
        var filter = statuses is { Count: > 0 }
            ? Builders<IntentDocument>.Filter.In(d => d.Status, statuses)
            : Builders<IntentDocument>.Filter.Empty;
        // Default sort = sort_key ASC: top of the list is the lexicographically smallest key.
        // Frontend renders this order directly so DnD reorders feed straight back through.
        var documents = session is null
            ? await _intents.Find(filter)
                .SortBy(d => d.SortKey)
                .ToListAsync(ct)
            : await _intents.Find(session, filter)
                .SortBy(d => d.SortKey)
                .ToListAsync(ct);

        var result = new List<Intent>(documents.Count);
        foreach (var doc in documents)
        {
            result.Add(IntentDocumentMapper.ToDomain(doc));
        }
        return result;
    }

    public async Task<IntentListPage> ListPagedAsync(IntentListSpec spec, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var session = sessions.Current;
        var fb = Builders<IntentDocument>.Filter;
        var clauses = new List<FilterDefinition<IntentDocument>>();

        if (spec.Statuses is { Count: > 0 })
        {
            clauses.Add(fb.In(d => d.Status, spec.Statuses));
        }

        if (spec.TagId is not null)
        {
            clauses.Add(fb.AnyEq(d => d.TagIds, spec.TagId.Value.Value));
        }

        if (spec.Untagged)
        {
            clauses.Add(fb.Size(d => d.TagIds, 0));
        }

        // Restrict to an externally-resolved id set (the live tmux session set for the
        // `terminal_running` filter). The handler short-circuits an empty set, so an empty
        // list here would only ever come from a caller passing one intentionally.
        if (spec.Ids is { Count: > 0 })
        {
            clauses.Add(fb.In(d => d.Id, spec.Ids));
        }

        if (spec.Pinned)
        {
            var pinnedIds = await ListPinnedIntentIdsAsync(session, ct);
            if (pinnedIds.Count == 0)
            {
                return new IntentListPage([], NextCursor: null);
            }
            clauses.Add(fb.In(d => d.Id, pinnedIds));
        }

        if (!string.IsNullOrEmpty(spec.Query))
        {
            // Case-insensitive substring match. Uses regex with collection scan; OK for MVP scale.
            var pattern = System.Text.RegularExpressions.Regex.Escape(spec.Query);
            clauses.Add(fb.Regex(d => d.Text, new MongoDB.Bson.BsonRegularExpression(pattern, "i")));
        }

        if (spec.Cursor is not null)
        {
            clauses.Add(IntentListQueryBuilder.BuildCursorFilter(spec.Sort, spec.Cursor));
        }

        var filter = clauses.Count == 0 ? fb.Empty : fb.And(clauses);
        var sort = IntentListQueryBuilder.BuildSort(spec.Sort);

        var find = session is null
            ? _intents.Find(filter).Sort(sort)
            : _intents.Find(session, filter).Sort(sort);

        var pageSize = spec.Limit + 1;
        var documents = await find.Limit(pageSize).ToListAsync(ct);

        var hasMore = documents.Count > spec.Limit;
        var pageDocs = hasMore ? documents.Take(spec.Limit).ToList() : documents;

        var items = new List<Intent>(pageDocs.Count);
        foreach (var doc in pageDocs)
        {
            items.Add(IntentDocumentMapper.ToDomain(doc));
        }

        IntentListCursor? next = null;
        if (hasMore && pageDocs.Count > 0)
        {
            next = IntentListQueryBuilder.BuildNextCursor(spec.Sort, pageDocs[^1]);
        }

        return new IntentListPage(items, next);
    }

    // Distinct intent ids pinned into any context. Pins live in a separate
    // collection (intent_pins) with no denormalised flag on the intent, so the
    // "pinned" list filter resolves the (bounded) id set first.
    private async Task<IReadOnlyList<string>> ListPinnedIntentIdsAsync(
        IClientSessionHandle? session,
        CancellationToken ct)
    {
        var all = Builders<IntentPinDocument>.Filter.Empty;
        var ids = session is null
            ? await _pins.Distinct(d => d.IntentId, all, cancellationToken: ct).ToListAsync(ct)
            : await _pins.Distinct(session, d => d.IntentId, all, cancellationToken: ct).ToListAsync(ct);
        return ids;
    }

    public async Task<string?> GetMinSortKeyAsync(CancellationToken ct)
    {
        var session = sessions.Current;
        var all = Builders<IntentDocument>.Filter.Empty;
        var find = session is null
            ? _intents.Find(all)
            : _intents.Find(session, all);
        var doc = await find
            .Sort(Builders<IntentDocument>.Sort.Ascending(d => d.SortKey))
            .Limit(1)
            .Project(d => d.SortKey)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrEmpty(doc) ? null : doc;
    }
}
