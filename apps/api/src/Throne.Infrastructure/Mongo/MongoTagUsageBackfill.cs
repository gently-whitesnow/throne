using MongoDB.Bson;
using MongoDB.Driver;

namespace Throne.Infrastructure.Mongo;

// One-off seed of TagDocument.usage_count for tags created before the counter-cache
// existed. Idempotent: only touches tags that have no `usage_count` field yet, so it
// no-ops on every restart once the backfill has run.
internal static class MongoTagUsageBackfill
{
    public static async Task RunAsync(IMongoDatabase database, CancellationToken ct)
    {
        var tags = database.GetCollection<BsonDocument>(MongoCollectionNames.Tags);
        var pending = await tags
            .Find(Builders<BsonDocument>.Filter.Exists("usage_count", false))
            .Project(Builders<BsonDocument>.Projection.Include("_id"))
            .ToListAsync(ct);
        if (pending.Count == 0)
        {
            return;
        }

        var intents = database.GetCollection<BsonDocument>(MongoCollectionNames.Intents);
        var grouped = await intents
            .Aggregate()
            .Unwind("tag_ids")
            .Group(new BsonDocument { { "_id", "$tag_ids" }, { "count", new BsonDocument("$sum", 1) } })
            .ToListAsync(ct);
        var counts = grouped.ToDictionary(d => d["_id"].AsString, d => d["count"].ToInt32());

        foreach (var doc in pending)
        {
            var id = doc["_id"].AsString;
            var count = counts.GetValueOrDefault(id, 0);
            await tags.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", id),
                Builders<BsonDocument>.Update.Set("usage_count", count),
                cancellationToken: ct);
        }
    }
}
