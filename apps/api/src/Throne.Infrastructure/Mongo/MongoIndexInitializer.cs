using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using Throne.Application.Auth;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoIndexInitializer(IMongoDatabase database) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        ExecuteWhenPrimaryAsync(async ct =>
        {
            await BackfillOwnerUserIdAsync(ct);
            await CreateIndexesAsync(ct);
        }, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static readonly IReadOnlyList<string> UserOwnedCollections =
    [
        MongoCollectionNames.Intents,
        MongoCollectionNames.IntentQa,
        MongoCollectionNames.IntentReview,
        MongoCollectionNames.IntentAttachments,
        MongoCollectionNames.DreamRuns,
        MongoCollectionNames.PersonalAccessTokens,
    ];

    private async Task BackfillOwnerUserIdAsync(CancellationToken cancellationToken)
    {
        var filter = new BsonDocument("owner_user_id", new BsonDocument("$exists", false));
        var update = new BsonDocument("$set", new BsonDocument("owner_user_id", CurrentUserIds.LocalDev));
        foreach (var name in UserOwnedCollections)
        {
            var collection = database.GetCollection<BsonDocument>(name);
            await collection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        }
    }

    private async Task CreateIndexesAsync(CancellationToken cancellationToken)
    {
        var textVersions = database.GetCollection<TextVersionDocument>(MongoCollectionNames.TextVersions);
        await textVersions.Indexes.CreateOneAsync(
            new CreateIndexModel<TextVersionDocument>(
                Builders<TextVersionDocument>.IndexKeys
                    .Ascending(x => x.OwnerKind)
                    .Ascending(x => x.OwnerId)
                    .Ascending(x => x.Version),
                new CreateIndexOptions { Unique = true, Name = "owner_version_unique" }),
            cancellationToken: cancellationToken);

        var qa = database.GetCollection<IntentQaDocument>(MongoCollectionNames.IntentQa);
        await qa.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<IntentQaDocument>(
                    Builders<IntentQaDocument>.IndexKeys
                        .Ascending(x => x.IntentId)
                        .Ascending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "intent_created" }),
                new CreateIndexModel<IntentQaDocument>(
                    Builders<IntentQaDocument>.IndexKeys.Ascending(x => x.OwnerUserId),
                    new CreateIndexOptions { Name = "owner_user_id" }),
            ],
            cancellationToken);

        var reviews = database.GetCollection<IntentReviewDocument>(MongoCollectionNames.IntentReview);
        await reviews.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<IntentReviewDocument>(
                    Builders<IntentReviewDocument>.IndexKeys
                        .Ascending(x => x.IntentId)
                        .Ascending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "intent_created" }),
                new CreateIndexModel<IntentReviewDocument>(
                    Builders<IntentReviewDocument>.IndexKeys.Ascending(x => x.OwnerUserId),
                    new CreateIndexOptions { Name = "owner_user_id" }),
            ],
            cancellationToken);

        var statusChanges = database.GetCollection<IntentStatusChangeDocument>(MongoCollectionNames.IntentStatusChanges);
        await statusChanges.Indexes.CreateOneAsync(
            new CreateIndexModel<IntentStatusChangeDocument>(
                Builders<IntentStatusChangeDocument>.IndexKeys
                    .Ascending(x => x.IntentId)
                    .Ascending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "intent_created" }),
            cancellationToken: cancellationToken);

        var intentAttachments = database.GetCollection<IntentAttachmentDocument>(MongoCollectionNames.IntentAttachments);
        await intentAttachments.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<IntentAttachmentDocument>(
                    Builders<IntentAttachmentDocument>.IndexKeys.Ascending(x => x.IntentId),
                    new CreateIndexOptions { Name = "intent_id" }),
                new CreateIndexModel<IntentAttachmentDocument>(
                    Builders<IntentAttachmentDocument>.IndexKeys.Ascending(x => x.OwnerUserId),
                    new CreateIndexOptions { Name = "owner_user_id" }),
            ],
            cancellationToken);

        var tags = database.GetCollection<TagDocument>(MongoCollectionNames.Tags);
        await tags.Indexes.CreateOneAsync(
            new CreateIndexModel<TagDocument>(
                Builders<TagDocument>.IndexKeys.Ascending(x => x.Name),
                new CreateIndexOptions { Unique = true, Name = "name_unique" }),
            cancellationToken: cancellationToken);

        var intentsCollection = database.GetCollection<IntentDocument>(MongoCollectionNames.Intents);
        await intentsCollection.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<IntentDocument>(
                    Builders<IntentDocument>.IndexKeys.Ascending(x => x.TagIds),
                    new CreateIndexOptions { Name = "tag_ids" }),
                new CreateIndexModel<IntentDocument>(
                    Builders<IntentDocument>.IndexKeys.Ascending(x => x.OwnerUserId),
                    new CreateIndexOptions { Name = "owner_user_id" }),
            ],
            cancellationToken);

        var dreamRuns = database.GetCollection<DreamRunDocument>(MongoCollectionNames.DreamRuns);
        await dreamRuns.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<DreamRunDocument>(
                    Builders<DreamRunDocument>.IndexKeys
                        .Ascending(x => x.Status)
                        .Ascending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "status_created" }),
                new CreateIndexModel<DreamRunDocument>(
                    Builders<DreamRunDocument>.IndexKeys
                        .Ascending("intent_refs.intent_id"),
                    new CreateIndexOptions { Name = "intent_refs_lookup" }),
                new CreateIndexModel<DreamRunDocument>(
                    Builders<DreamRunDocument>.IndexKeys.Ascending(x => x.OwnerUserId),
                    new CreateIndexOptions { Name = "owner_user_id" }),
            ],
            cancellationToken);

        var pat = database.GetCollection<PersonalAccessTokenDocument>(MongoCollectionNames.PersonalAccessTokens);
        await pat.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<PersonalAccessTokenDocument>(
                    Builders<PersonalAccessTokenDocument>.IndexKeys.Ascending(x => x.HashSha256),
                    new CreateIndexOptions { Unique = true, Name = "hash_sha256_unique" }),
                new CreateIndexModel<PersonalAccessTokenDocument>(
                    Builders<PersonalAccessTokenDocument>.IndexKeys.Ascending(x => x.OwnerUserId),
                    new CreateIndexOptions { Unique = true, Name = "owner_user_id_unique" }),
            ],
            cancellationToken);

        var calls = database.GetCollection<McpCallLogDocument>(MongoCollectionNames.McpCallLog);
        await calls.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<McpCallLogDocument>(
                    Builders<McpCallLogDocument>.IndexKeys
                        .Ascending(x => x.IntentId)
                        .Ascending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "intent_created" }),
                new CreateIndexModel<McpCallLogDocument>(
                    Builders<McpCallLogDocument>.IndexKeys
                        .Ascending(x => x.SessionId)
                        .Ascending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "session_created" }),
                new CreateIndexModel<McpCallLogDocument>(
                    Builders<McpCallLogDocument>.IndexKeys
                        .Ascending(x => x.ToolName)
                        .Ascending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "tool_created" }),
                new CreateIndexModel<McpCallLogDocument>(
                    Builders<McpCallLogDocument>.IndexKeys.Ascending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "created_at" }),
            ],
            cancellationToken);
    }

    private static async Task ExecuteWhenPrimaryAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        const int attempts = 10;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await operation(cancellationToken);
                return;
            }
            catch (MongoNotPrimaryException) when (attempt < attempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
        }
    }
}
