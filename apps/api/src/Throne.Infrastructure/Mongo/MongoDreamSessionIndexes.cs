using MongoDB.Driver;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Indexes for the <c>dream_sessions</c> collection. Multi-tenancy filter
/// (owner_user_id) is the primary key for every read; (owner, created_at desc)
/// backs paginated listings; (owner, vendor, created_at desc) backs the vendor
/// filter; (owner, host, created_at desc) backs the per-machine frontier filter
/// used by the dream-mode agent on the MCP list / HTTP list paths.
/// </summary>
internal static class MongoDreamSessionIndexes
{
    public static async Task CreateAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<DreamSessionDocument>(MongoCollectionNames.DreamSessions);
        await collection.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<DreamSessionDocument>(
                    Builders<DreamSessionDocument>.IndexKeys.Ascending(x => x.OwnerUserId),
                    new CreateIndexOptions { Name = "owner_user_id" }),
                new CreateIndexModel<DreamSessionDocument>(
                    Builders<DreamSessionDocument>.IndexKeys
                        .Ascending(x => x.OwnerUserId)
                        .Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "owner_created_desc" }),
                new CreateIndexModel<DreamSessionDocument>(
                    Builders<DreamSessionDocument>.IndexKeys
                        .Ascending(x => x.OwnerUserId)
                        .Ascending(x => x.Vendor)
                        .Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "owner_vendor_created" }),
                new CreateIndexModel<DreamSessionDocument>(
                    Builders<DreamSessionDocument>.IndexKeys
                        .Ascending(x => x.OwnerUserId)
                        .Ascending(x => x.Host)
                        .Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "owner_host_created" }),
            ],
            cancellationToken);
    }
}
