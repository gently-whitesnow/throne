using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Throne.Application.Tags;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

// Keyset-paged tag reader (fixed `last_attached_at desc, _id asc` order). Split
// out of MongoTagRepository to keep that file focused on the single-tag write-path.
internal sealed class MongoTagListReader(IMongoDatabase database, MongoSessionAccessor sessions)
{
    private const string SortAttachedAtField = "__sort_attached_at";
    private const string LastAttachedAtField = "last_attached_at";
    private const string CreatedAtField = "created_at";

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

        var filter = clauses.Count == 0 ? fb.Empty : fb.And(clauses);
        var aggregate = session is null
            ? _tags.Aggregate().Match(filter).AppendStage<BsonDocument>(BuildSortKeyStage())
            : _tags.Aggregate(session).Match(filter).AppendStage<BsonDocument>(BuildSortKeyStage());

        if (spec.Cursor is not null)
        {
            aggregate = aggregate.Match(BuildCursorFilter(spec.Cursor));
        }

        var documents = await aggregate
            .Sort(Builders<BsonDocument>.Sort.Descending(SortAttachedAtField).Ascending("_id"))
            .Limit(spec.Limit + 1)
            .ToListAsync(ct);

        var hasMore = documents.Count > spec.Limit;
        var pageDocs = hasMore ? documents.Take(spec.Limit).ToList() : documents;

        var items = new List<TagListItem>(pageDocs.Count);
        foreach (var doc in pageDocs)
        {
            items.Add(new TagListItem(MongoTagMapper.ToDomain(ToTagDocument(doc))));
        }

        TagListCursor? next = hasMore && pageDocs.Count > 0
            ? new TagListCursor(ReadSortKey(pageDocs[^1]), pageDocs[^1]["_id"].AsString)
            : null;

        return new TagListPage(items, next);
    }

    private static BsonDocument BuildSortKeyStage() =>
        new("$addFields", new BsonDocument(
            SortAttachedAtField,
            new BsonDocument("$ifNull", new BsonArray
            {
                $"${LastAttachedAtField}",
                $"${CreatedAtField}",
            })));

    private static FilterDefinition<BsonDocument> BuildCursorFilter(TagListCursor cursor)
    {
        var fb = Builders<BsonDocument>.Filter;
        var lastAttachedAt = cursor.LastAttachedAt.ToUniversalTime();
        return fb.Or(
            fb.Lt(SortAttachedAtField, lastAttachedAt),
            fb.And(
                fb.Eq(SortAttachedAtField, lastAttachedAt),
                fb.Gt("_id", cursor.Id)));
    }

    private static TagDocument ToTagDocument(BsonDocument doc)
    {
        var clone = (BsonDocument)doc.Clone();
        clone.Remove(SortAttachedAtField);
        return BsonSerializer.Deserialize<TagDocument>(clone);
    }

    private static DateTime ReadSortKey(BsonDocument doc) =>
        DateTime.SpecifyKind(doc[SortAttachedAtField].ToUniversalTime(), DateTimeKind.Utc);
}
