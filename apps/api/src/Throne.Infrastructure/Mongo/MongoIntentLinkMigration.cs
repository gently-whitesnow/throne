using MongoDB.Bson;
using MongoDB.Driver;

namespace Throne.Infrastructure.Mongo;

internal static class MongoIntentLinkMigration
{
    private const string Blocks = "blocks";
    private const string DerivedFrom = "derived_from";
    private const string Relates = "relates";
    private const string DuplicateOf = "duplicate_of";

    public static async Task RunAsync(IMongoDatabase database, CancellationToken ct)
    {
        await MigrateLinksAsync(database.GetCollection<BsonDocument>(MongoCollectionNames.IntentLinks), ct);
        await MigrateEventPayloadsAsync(database.GetCollection<BsonDocument>(MongoCollectionNames.IntentEvents), ct);
    }

    public static async Task DropOldUniqueIndexAsync(IMongoDatabase database, CancellationToken ct)
    {
        var links = database.GetCollection<BsonDocument>(MongoCollectionNames.IntentLinks);
        try
        {
            await links.Indexes.DropOneAsync("from_to_type_unique", ct);
        }
        catch (MongoCommandException ex) when (ex.CodeName == "IndexNotFound" || ex.Code == 27)
        {
        }
    }

    private static async Task MigrateLinksAsync(IMongoCollection<BsonDocument> links, CancellationToken ct)
    {
        var docs = await links.Find(Builders<BsonDocument>.Filter.Exists("type")).ToListAsync(ct);
        if (docs.Count == 0)
        {
            return;
        }

        var targets = new List<LinkTarget>();
        var deleteIds = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < docs.Count; i += 1)
        {
            var doc = docs[i];
            var id = doc.GetValue("_id").AsString;
            var from = doc.GetValue("from_id").AsString;
            var to = doc.GetValue("to_id").AsString;
            switch (doc.GetValue("type").AsString)
            {
                case Blocks:
                    targets.Add(new LinkTarget(id, from, to, Blocking: true, i));
                    break;
                case DerivedFrom:
                    targets.Add(new LinkTarget(id, to, from, Blocking: false, i));
                    break;
                case Relates:
                case DuplicateOf:
                    deleteIds.Add(id);
                    break;
                default:
                    deleteIds.Add(id);
                    break;
            }
        }

        var keep = targets
            .GroupBy(t => $"{t.FromId}\u001F{t.ToId}", StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(t => t.Blocking).ThenBy(t => t.Ordinal).First())
            .ToList();
        var keepIds = keep.Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var target in targets)
        {
            if (!keepIds.Contains(target.Id))
            {
                deleteIds.Add(target.Id);
            }
        }

        if (deleteIds.Count > 0)
        {
            await links.DeleteManyAsync(
                Builders<BsonDocument>.Filter.In("_id", deleteIds),
                ct);
        }

        foreach (var target in keep)
        {
            await links.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", target.Id),
                Builders<BsonDocument>.Update
                    .Set("from_id", target.FromId)
                    .Set("to_id", target.ToId)
                    .Set("blocking", target.Blocking)
                    .Unset("type"),
                cancellationToken: ct);
        }
    }

    private static async Task MigrateEventPayloadsAsync(IMongoCollection<BsonDocument> events, CancellationToken ct)
    {
        var docs = await events.Find(Builders<BsonDocument>.Filter.Exists("link.type")).ToListAsync(ct);
        foreach (var doc in docs)
        {
            var link = doc.GetValue("link").AsBsonDocument;
            var type = link.GetValue("type").AsString;
            var from = link.GetValue("from_id").AsString;
            var to = link.GetValue("to_id").AsString;
            var update = Builders<BsonDocument>.Update.Unset("link.type");
            if (type == DerivedFrom)
            {
                update = update
                    .Set("intent_id", to)
                    .Set("peer_intent_id", from)
                    .Set("link.from_id", to)
                    .Set("link.to_id", from)
                    .Set("link.blocking", false);
            }
            else
            {
                update = update.Set("link.blocking", type == Blocks);
            }

            await events.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", doc.GetValue("_id")),
                update,
                cancellationToken: ct);
        }
    }

    private sealed record LinkTarget(string Id, string FromId, string ToId, bool Blocking, int Ordinal);
}
