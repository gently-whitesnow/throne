using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoIndexInitializer(IMongoDatabase database) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        ExecuteWhenPrimaryAsync(CreateIndexesAsync, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var qa = database.GetCollection<IntentQaDocument>(MongoCollectionNames.IntentQa);
        await qa.Indexes.CreateOneAsync(
            new CreateIndexModel<IntentQaDocument>(
                Builders<IntentQaDocument>.IndexKeys
                    .Ascending(x => x.IntentId)
                    .Ascending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "intent_created" }),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var reviews = database.GetCollection<IntentReviewDocument>(MongoCollectionNames.IntentReview);
        await reviews.Indexes.CreateOneAsync(
            new CreateIndexModel<IntentReviewDocument>(
                Builders<IntentReviewDocument>.IndexKeys
                    .Ascending(x => x.IntentId)
                    .Ascending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "intent_created" }),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var statusChanges = database.GetCollection<IntentStatusChangeDocument>(MongoCollectionNames.IntentStatusChanges);
        await statusChanges.Indexes.CreateOneAsync(
            new CreateIndexModel<IntentStatusChangeDocument>(
                Builders<IntentStatusChangeDocument>.IndexKeys
                    .Ascending(x => x.IntentId)
                    .Ascending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "intent_created" }),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var intentAttachments = database.GetCollection<IntentAttachmentDocument>(MongoCollectionNames.IntentAttachments);
        await intentAttachments.Indexes.CreateOneAsync(
            new CreateIndexModel<IntentAttachmentDocument>(
                Builders<IntentAttachmentDocument>.IndexKeys.Ascending(x => x.IntentId),
                new CreateIndexOptions { Name = "intent_id" }),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var tags = database.GetCollection<TagDocument>(MongoCollectionNames.Tags);
        await tags.Indexes.CreateOneAsync(
            new CreateIndexModel<TagDocument>(
                Builders<TagDocument>.IndexKeys.Ascending(x => x.Name),
                new CreateIndexOptions { Unique = true, Name = "name_unique" }),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var intentsByTag = database.GetCollection<IntentDocument>(MongoCollectionNames.Intents);
        await intentsByTag.Indexes.CreateOneAsync(
            new CreateIndexModel<IntentDocument>(
                Builders<IntentDocument>.IndexKeys.Ascending(x => x.TagIds),
                new CreateIndexOptions { Name = "tag_ids" }),
            cancellationToken: cancellationToken).ConfigureAwait(false);

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
                    Builders<DreamRunDocument>.IndexKeys
                        .Ascending(x => x.Status)
                        .Descending(x => x.ClosedAt),
                    new CreateIndexOptions { Name = "status_closed_desc" }),
            ],
            cancellationToken).ConfigureAwait(false);

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
            cancellationToken).ConfigureAwait(false);
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
                await operation(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (MongoNotPrimaryException) when (attempt < attempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
