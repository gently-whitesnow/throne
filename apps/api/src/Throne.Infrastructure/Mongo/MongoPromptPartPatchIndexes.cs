using MongoDB.Bson;
using MongoDB.Driver;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Indexes for the <c>prompt_part_patches</c> collection. (created_at desc) backs paginated
/// listings; (status, created_at desc) and (target_scope, target_key, created_at desc) back the
/// filter combinations used by <c>list_prompt_part_patches</c>.
/// </summary>
internal static class MongoPromptPartPatchIndexes
{
    public static async Task CreateAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<PromptPartPatchDocument>(MongoCollectionNames.PromptPartPatches);
        await collection.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<PromptPartPatchDocument>(
                    Builders<PromptPartPatchDocument>.IndexKeys.Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "created_desc" }),
                new CreateIndexModel<PromptPartPatchDocument>(
                    Builders<PromptPartPatchDocument>.IndexKeys
                        .Ascending(x => x.Status)
                        .Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "status_created" }),
                new CreateIndexModel<PromptPartPatchDocument>(
                    Builders<PromptPartPatchDocument>.IndexKeys
                        .Ascending(x => x.TargetScope)
                        .Ascending(x => x.TargetKey)
                        .Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "target_created" }),
                // Sparse-unique on idempotency_key: only docs that actually carry a key
                // participate, so legacy rows without one are unaffected.
                new CreateIndexModel<PromptPartPatchDocument>(
                    Builders<PromptPartPatchDocument>.IndexKeys.Ascending(x => x.IdempotencyKey),
                    new CreateIndexOptions<PromptPartPatchDocument>
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
