using MongoDB.Driver;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Indexes for the <c>dream_sessions</c> collection. (created_at desc) backs
/// paginated listings; (vendor, created_at desc) backs the vendor filter;
/// (host, created_at desc) backs the per-machine frontier filter used by the
/// dream-mode agent on the MCP list / HTTP list paths.
/// </summary>
internal static class MongoDreamSessionIndexes
{
    public static async Task CreateAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<DreamSessionDocument>(MongoCollectionNames.DreamSessions);
        await collection.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<DreamSessionDocument>(
                    Builders<DreamSessionDocument>.IndexKeys.Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "created_desc" }),
                new CreateIndexModel<DreamSessionDocument>(
                    Builders<DreamSessionDocument>.IndexKeys
                        .Ascending(x => x.Vendor)
                        .Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "vendor_created" }),
                new CreateIndexModel<DreamSessionDocument>(
                    Builders<DreamSessionDocument>.IndexKeys
                        .Ascending(x => x.Host)
                        .Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "host_created" }),
            ],
            cancellationToken);
    }
}
