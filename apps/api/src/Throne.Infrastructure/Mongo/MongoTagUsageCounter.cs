using MongoDB.Driver;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

// Transactional maintenance of TagDocument.usage_count from the intent write-path.
// Always runs inside the caller's session so the counter moves atomically with the
// tag↔intent binding change.
internal static class MongoTagUsageCounter
{
    public static Task ApplyAsync(
        IMongoCollection<TagDocument> tags,
        IClientSessionHandle session,
        IReadOnlyCollection<string> tagIds,
        int delta,
        CancellationToken ct)
    {
        if (tagIds.Count == 0 || delta == 0)
        {
            return Task.CompletedTask;
        }

        var filter = Builders<TagDocument>.Filter.In(d => d.Id, tagIds);
        var update = Builders<TagDocument>.Update.Inc(d => d.UsageCount, delta);
        return tags.UpdateManyAsync(session, filter, update, options: null, ct);
    }
}
