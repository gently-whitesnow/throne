using MongoDB.Bson;
using MongoDB.Driver;
using Throne.Application.Tags;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

// Keyset-paged tag reader (fixed `usage_count desc, _id asc` order). Split out of
// MongoTagRepository to keep that file focused on the single-tag write-path.
internal sealed class MongoTagListReader(IMongoDatabase database, MongoSessionAccessor sessions)
{
    private readonly IMongoCollection<TagDocument> _tags =
        database.GetCollection<TagDocument>(MongoCollectionNames.Tags);

    public async Task<TagListPage> ListPageAsync(TagListSpec spec, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var session = sessions.Current;
        var fb = Builders<TagDocument>.Filter;
        var clauses = new List<FilterDefinition<TagDocument>>();

        if (!string.IsNullOrEmpty(spec.Search))
        {
            var pattern = System.Text.RegularExpressions.Regex.Escape(spec.Search);
            clauses.Add(fb.Regex(d => d.Name, new BsonRegularExpression(pattern, "i")));
        }

        if (spec.Cursor is not null)
        {
            var c = spec.Cursor;
            clauses.Add(fb.Or(
                fb.Lt(d => d.UsageCount, c.UsageCount),
                fb.And(fb.Eq(d => d.UsageCount, c.UsageCount), fb.Gt(d => d.Id, c.Id))));
        }

        var filter = clauses.Count == 0 ? fb.Empty : fb.And(clauses);
        var sort = Builders<TagDocument>.Sort.Descending(d => d.UsageCount).Ascending(d => d.Id);

        var find = session is null
            ? _tags.Find(filter).Sort(sort)
            : _tags.Find(session, filter).Sort(sort);

        var documents = await find.Limit(spec.Limit + 1).ToListAsync(ct);

        var hasMore = documents.Count > spec.Limit;
        var pageDocs = hasMore ? documents.Take(spec.Limit).ToList() : documents;

        var items = new List<TagListItem>(pageDocs.Count);
        foreach (var doc in pageDocs)
        {
            items.Add(new TagListItem(MongoTagMapper.ToDomain(doc), doc.UsageCount));
        }

        TagListCursor? next = hasMore && pageDocs.Count > 0
            ? new TagListCursor(pageDocs[^1].UsageCount, pageDocs[^1].Id)
            : null;

        return new TagListPage(items, next);
    }
}
