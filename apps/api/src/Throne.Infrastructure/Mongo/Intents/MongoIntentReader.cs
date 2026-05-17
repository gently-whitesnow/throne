using MongoDB.Driver;
using Throne.Application.Auth;
using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Intents;

internal sealed class MongoIntentReader(
    IMongoDatabase database,
    MongoSessionAccessor sessions,
    ICurrentUserAccessor currentUser)
{
    private readonly IMongoCollection<IntentDocument> _intents =
        database.GetCollection<IntentDocument>(MongoCollectionNames.Intents);

    public async Task<Intent?> GetByIdAsync(IntentId id, CancellationToken ct)
    {
        var session = sessions.Current;
        var byIdAndOwner = IntentCollectionFilters.ByIdAndOwner(id.Value, currentUser.UserId);
        var document = session is null
            ? await _intents.Find(byIdAndOwner).FirstOrDefaultAsync(ct)
            : await _intents.Find(session, byIdAndOwner).FirstOrDefaultAsync(ct);

        return document is null ? null : IntentDocumentMapper.ToDomain(document);
    }

    public async Task<IReadOnlyList<Intent>> ListAsync(IReadOnlyList<string>? statuses, CancellationToken ct)
    {
        var session = sessions.Current;
        var ownerFilter = IntentCollectionFilters.Owner(currentUser.UserId);
        var filter = statuses is { Count: > 0 }
            ? Builders<IntentDocument>.Filter.And(
                ownerFilter,
                Builders<IntentDocument>.Filter.In(d => d.Status, statuses))
            : ownerFilter;
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
        var clauses = new List<FilterDefinition<IntentDocument>> { IntentCollectionFilters.Owner(currentUser.UserId) };

        if (spec.Statuses is { Count: > 0 })
        {
            clauses.Add(fb.In(d => d.Status, spec.Statuses));
        }

        if (spec.TagId is not null)
        {
            clauses.Add(fb.AnyEq(d => d.TagIds, spec.TagId.Value.Value));
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

        var filter = fb.And(clauses);
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

    public async Task<string?> GetMinSortKeyAsync(CancellationToken ct)
    {
        var session = sessions.Current;
        var ownerFilter = IntentCollectionFilters.Owner(currentUser.UserId);
        var find = session is null
            ? _intents.Find(ownerFilter)
            : _intents.Find(session, ownerFilter);
        var doc = await find
            .Sort(Builders<IntentDocument>.Sort.Ascending(d => d.SortKey))
            .Limit(1)
            .Project(d => d.SortKey)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrEmpty(doc) ? null : doc;
    }
}
