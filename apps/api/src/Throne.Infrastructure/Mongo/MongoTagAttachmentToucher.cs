using MongoDB.Driver;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal static class MongoTagAttachmentToucher
{
    public static Task TouchAsync(
        IMongoCollection<TagDocument> tags,
        IClientSessionHandle session,
        IReadOnlyCollection<string> tagIds,
        DateTimeOffset attachedAt,
        CancellationToken ct)
    {
        if (tagIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        var filter = Builders<TagDocument>.Filter.In(d => d.Id, tagIds);
        var update = Builders<TagDocument>.Update.Set(d => d.LastAttachedAt, attachedAt.UtcDateTime);
        return tags.UpdateManyAsync(session, filter, update, options: null, ct);
    }
}
