using MongoDB.Bson;
using MongoDB.Driver;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Indexes for the <c>instruction_patches</c> collection. Multi-tenancy filter
/// (owner_user_id) is the primary key for every read; (owner, created_at desc)
/// backs paginated listings; (owner, status) and (owner, target_kind) back the
/// MCP filter combinations used by <c>list_instruction_patches</c>.
/// </summary>
internal static class MongoInstructionPatchIndexes
{
    public static async Task CreateAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<InstructionPatchDocument>(MongoCollectionNames.InstructionPatches);
        await collection.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<InstructionPatchDocument>(
                    Builders<InstructionPatchDocument>.IndexKeys.Ascending(x => x.OwnerUserId),
                    new CreateIndexOptions { Name = "owner_user_id" }),
                new CreateIndexModel<InstructionPatchDocument>(
                    Builders<InstructionPatchDocument>.IndexKeys
                        .Ascending(x => x.OwnerUserId)
                        .Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "owner_created_desc" }),
                new CreateIndexModel<InstructionPatchDocument>(
                    Builders<InstructionPatchDocument>.IndexKeys
                        .Ascending(x => x.OwnerUserId)
                        .Ascending(x => x.Status)
                        .Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "owner_status_created" }),
                new CreateIndexModel<InstructionPatchDocument>(
                    Builders<InstructionPatchDocument>.IndexKeys
                        .Ascending(x => x.OwnerUserId)
                        .Ascending(x => x.TargetKind)
                        .Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "owner_target_created" }),
                // Sparse-unique on (owner, idempotency_key): only docs that actually
                // carry a key participate, so legacy rows without one are unaffected.
                // PartialFilterExpression keeps the index lean (Mongo skips docs that
                // don't match the filter) and gives the deduplication invariant teeth.
                new CreateIndexModel<InstructionPatchDocument>(
                    Builders<InstructionPatchDocument>.IndexKeys
                        .Ascending(x => x.OwnerUserId)
                        .Ascending(x => x.IdempotencyKey),
                    new CreateIndexOptions<InstructionPatchDocument>
                    {
                        Name = "owner_idempotency_key_unique",
                        Unique = true,
                        PartialFilterExpression = new BsonDocument(
                            "idempotency_key",
                            new BsonDocument("$type", "string")),
                    }),
            ],
            cancellationToken);
    }
}
