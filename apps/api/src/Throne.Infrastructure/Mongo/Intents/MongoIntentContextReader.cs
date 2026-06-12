using MongoDB.Bson;
using MongoDB.Driver;
using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Intents;

/// <summary>
/// Read-only aggregator for the context rail. Counts every bucket server-side via a single
/// <c>$facet</c> over all intents (status counts, untagged scopes, per-tag breakdowns)
/// plus a distinct on <c>intent_pins</c> for the pinned bucket — never loads the full list.
/// </summary>
internal sealed class MongoIntentContextReader(
    IMongoDatabase database,
    MongoSessionAccessor sessions)
{
    private readonly IMongoCollection<IntentDocument> _intents =
        database.GetCollection<IntentDocument>(MongoCollectionNames.Intents);

    private readonly IMongoCollection<IntentPinDocument> _pins =
        database.GetCollection<IntentPinDocument>(MongoCollectionNames.IntentPins);

    private static readonly string[] ActiveStatuses =
    [
        IntentStatusNames.Draft, IntentStatusNames.Interview, IntentStatusNames.ReadyForWork,
        IntentStatusNames.Work, IntentStatusNames.ReadyForReview, IntentStatusNames.AwaitingOperator,
    ];

    private static readonly string[] ArchiveStatuses =
    [
        IntentStatusNames.Done, IntentStatusNames.Reject,
    ];

    public async Task<IntentContextCounts> GetContextCountsAsync(
        IReadOnlyList<string> runningTerminalIds, CancellationToken ct)
    {
        var session = sessions.Current;

        var pipeline = new BsonDocument[]
        {
            new("$facet", new BsonDocument
            {
                ["byStatus"] = new BsonArray
                {
                    new BsonDocument("$group", new BsonDocument
                    {
                        ["_id"] = "$status",
                        ["n"] = new BsonDocument("$sum", 1),
                    }),
                },
                ["activeUntagged"] = UntaggedFacet(ActiveStatuses),
                ["archiveUntagged"] = UntaggedFacet(ArchiveStatuses),
                ["activeTags"] = TagFacet(ActiveStatuses),
                ["archiveTags"] = TagFacet(ArchiveStatuses),
            }),
        };

        var cursor = session is null
            ? await _intents.AggregateAsync<BsonDocument>(pipeline, cancellationToken: ct)
            : await _intents.AggregateAsync<BsonDocument>(session, pipeline, cancellationToken: ct);
        var facet = await cursor.FirstOrDefaultAsync(ct);

        var pinned = await CountPinnedAsync(session, ct);
        var terminalRunning = await CountByIdsAsync(session, runningTerminalIds, ct);

        if (facet is null)
        {
            return new IntentContextCounts(0, 0, 0, 0, pinned, 0, 0, [], [], terminalRunning);
        }

        var byStatus = ReadStatusCounts(facet["byStatus"].AsBsonArray);

        return new IntentContextCounts(
            InboxReview: Lookup(byStatus, IntentStatusNames.ReadyForReview),
            InboxHelp: Lookup(byStatus, IntentStatusNames.AwaitingOperator),
            Fridge: Lookup(byStatus, IntentStatusNames.Fridge),
            Archive: Lookup(byStatus, IntentStatusNames.Done) + Lookup(byStatus, IntentStatusNames.Reject),
            Pinned: pinned,
            Untagged: ReadCount(facet["activeUntagged"].AsBsonArray),
            ArchiveUntagged: ReadCount(facet["archiveUntagged"].AsBsonArray),
            Tags: ReadTagCounts(facet["activeTags"].AsBsonArray),
            ArchiveTags: ReadTagCounts(facet["archiveTags"].AsBsonArray),
            TerminalRunning: terminalRunning);
    }

    // Count of intents whose id is in the live tmux session set (empty set → 0).
    private async Task<int> CountByIdsAsync(
        IClientSessionHandle? session, IReadOnlyList<string> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return 0;
        }
        var filter = Builders<IntentDocument>.Filter.In(d => d.Id, ids);
        var count = session is null
            ? await _intents.CountDocumentsAsync(filter, cancellationToken: ct)
            : await _intents.CountDocumentsAsync(session, filter, cancellationToken: ct);
        return (int)count;
    }

    private static BsonArray UntaggedFacet(string[] statuses) => new()
    {
        new BsonDocument("$match", new BsonDocument
        {
            ["status"] = new BsonDocument("$in", new BsonArray(statuses)),
            ["tag_ids"] = new BsonDocument("$size", 0),
        }),
        new BsonDocument("$count", "n"),
    };

    private static BsonArray TagFacet(string[] statuses) => new()
    {
        new BsonDocument("$match", new BsonDocument("status", new BsonDocument("$in", new BsonArray(statuses)))),
        new BsonDocument("$unwind", "$tag_ids"),
        new BsonDocument("$group", new BsonDocument
        {
            ["_id"] = "$tag_ids",
            ["n"] = new BsonDocument("$sum", 1),
        }),
    };

    private static Dictionary<string, int> ReadStatusCounts(BsonArray rows)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var doc = row.AsBsonDocument;
            if (doc["_id"].IsBsonNull)
            {
                continue;
            }
            map[doc["_id"].AsString] = doc["n"].ToInt32();
        }
        return map;
    }

    private static int Lookup(Dictionary<string, int> map, string status) =>
        map.TryGetValue(status, out var value) ? value : 0;

    // $count emits no document when the sub-pipeline matched nothing.
    private static int ReadCount(BsonArray rows) =>
        rows.Count == 0 ? 0 : rows[0].AsBsonDocument["n"].ToInt32();

    private static List<IntentTagCount> ReadTagCounts(BsonArray rows)
    {
        var list = new List<IntentTagCount>(rows.Count);
        foreach (var row in rows)
        {
            var doc = row.AsBsonDocument;
            if (doc["_id"].IsBsonNull)
            {
                continue;
            }
            list.Add(new IntentTagCount(doc["_id"].AsString, doc["n"].ToInt32()));
        }
        return list;
    }

    private async Task<int> CountPinnedAsync(IClientSessionHandle? session, CancellationToken ct)
    {
        var all = Builders<IntentPinDocument>.Filter.Empty;
        var ids = session is null
            ? await _pins.Distinct(d => d.IntentId, all, cancellationToken: ct).ToListAsync(ct)
            : await _pins.Distinct(session, d => d.IntentId, all, cancellationToken: ct).ToListAsync(ct);
        return ids.Count;
    }
}
