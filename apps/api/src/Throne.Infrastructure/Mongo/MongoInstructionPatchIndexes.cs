using MongoDB.Bson;
using MongoDB.Driver;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Indexes for the <c>instruction_patches</c> collection. (created_at desc)
/// backs paginated listings; (status, created_at desc) and
/// (target_kind, created_at desc) back the MCP filter combinations used by
/// <c>list_instruction_patches</c>.
/// </summary>
internal static class MongoInstructionPatchIndexes
{
    public static async Task CreateAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<InstructionPatchDocument>(MongoCollectionNames.InstructionPatches);
        await collection.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<InstructionPatchDocument>(
                    Builders<InstructionPatchDocument>.IndexKeys.Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "created_desc" }),
                new CreateIndexModel<InstructionPatchDocument>(
                    Builders<InstructionPatchDocument>.IndexKeys
                        .Ascending(x => x.Status)
                        .Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "status_created" }),
                new CreateIndexModel<InstructionPatchDocument>(
                    Builders<InstructionPatchDocument>.IndexKeys
                        .Ascending(x => x.TargetKind)
                        .Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "target_created" }),
                // Sparse-unique on idempotency_key: only docs that actually carry a
                // key participate, so legacy rows without one are unaffected. The
                // PartialFilterExpression keeps the index lean and gives the
                // deduplication invariant teeth.
                new CreateIndexModel<InstructionPatchDocument>(
                    Builders<InstructionPatchDocument>.IndexKeys.Ascending(x => x.IdempotencyKey),
                    new CreateIndexOptions<InstructionPatchDocument>
                    {
                        Name = "idempotency_key_unique",
                        Unique = true,
                        PartialFilterExpression = new BsonDocument(
                            "idempotency_key",
                            new BsonDocument("$type", "string")),
                    }),
            ],
            cancellationToken);
    }
}
