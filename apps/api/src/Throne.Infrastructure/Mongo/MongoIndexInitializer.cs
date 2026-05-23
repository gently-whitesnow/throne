using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using Throne.Application.Auth;
using Throne.Infrastructure.Mongo.Documents;
using Throne.Infrastructure.Mongo.Repositories;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoIndexInitializer(IMongoDatabase database) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        ExecuteWhenPrimaryAsync(async ct =>
        {
            await DropRetiredCollectionsAsync(ct);
            await BackfillOwnerUserIdAsync(ct);
            await CreateIndexesAsync(ct);
        }, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static readonly IReadOnlyList<string> UserOwnedCollections =
    [
        MongoCollectionNames.Intents,
        MongoCollectionNames.IntentAttachments,
        MongoCollectionNames.PersonalAccessTokens,
        MongoCollectionNames.InstructionPatches,
        MongoCollectionNames.DreamSessions,
        MongoCollectionNames.IntentPins,
    ];

    /// <summary>
    /// Collections retired by ADR-0021 (DreamRun aggregate replaced by
    /// InstructionPatch) and by the ADR-0022 demolition of the insight pipeline
    /// (chat_uploads / chat_conversations / insight_cards / analysis_jobs).
    /// Drop happens at boot inside the same primary-only hosted-service so it
    /// runs exactly once per deployment.
    /// </summary>
    private static readonly IReadOnlyList<string> RetiredCollections =
    [
        "dream_runs",
        "chat_uploads",
        "chat_conversations",
        "chat_messages",
        "insight_cards",
        "analysis_jobs",
    ];

    private async Task DropRetiredCollectionsAsync(CancellationToken cancellationToken)
    {
        var existing = await database
            .ListCollectionNames(cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);
        foreach (var name in RetiredCollections)
        {
            if (existing.Contains(name, StringComparer.Ordinal))
            {
                await database.DropCollectionAsync(name, cancellationToken);
            }
        }
    }

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
                new CreateIndexModel<IntentDocument>(
                    Builders<IntentDocument>.IndexKeys
                        .Ascending(x => x.OwnerUserId)
                        .Ascending(x => x.SortKey),
                    new CreateIndexOptions { Name = "owner_sort_key" }),
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

        await MongoInstructionPatchIndexes.CreateAsync(database, cancellationToken);
        await MongoDreamSessionIndexes.CreateAsync(database, cancellationToken);

        await CreateIntentLinkIndexesAsync(cancellationToken);
        await CreateIntentEventIndexesAsync(cancellationToken);
        await CreateIntentPinIndexesAsync(cancellationToken);
        await MongoIntentRepositoryBindingIndexes.CreateAsync(database, cancellationToken);

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

    private async Task CreateIntentEventIndexesAsync(CancellationToken cancellationToken)
    {
        var intentEvents = database.GetCollection<IntentEventDocument>(MongoCollectionNames.IntentEvents);
        await intentEvents.Indexes.CreateManyAsync(
            [
                // Primary lookup for the per-intent feed and migration idempotency.
                new CreateIndexModel<IntentEventDocument>(
                    Builders<IntentEventDocument>.IndexKeys
                        .Ascending(x => x.IntentId)
                        .Ascending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "intent_created" }),
                // Required so the OR-filter `intent_id = X OR peer_intent_id = X` stays
                // index-backed for the receiving end of a link event.
                new CreateIndexModel<IntentEventDocument>(
                    Builders<IntentEventDocument>.IndexKeys.Ascending(x => x.PeerIntentId),
                    new CreateIndexOptions { Name = "peer_intent_id" }),
                // Migration idempotency: skip insert if (intent_id, version) already
                // exists for kind=text_changed. The unique index also prevents accidental
                // double-writes if the migration races a fresh write-path edit.
                new CreateIndexModel<IntentEventDocument>(
                    Builders<IntentEventDocument>.IndexKeys
                        .Ascending(x => x.IntentId)
                        .Ascending(x => x.Version),
                    new CreateIndexOptions<IntentEventDocument>
                    {
                        Name = "intent_text_version_unique",
                        Unique = true,
                        // Restrict uniqueness to text_changed events — link events
                        // omit `version` and would otherwise collide on null.
                        PartialFilterExpression = Builders<IntentEventDocument>.Filter
                            .Eq(x => x.Kind, IntentEventKindWires.TextChanged),
                    }),
            ],
            cancellationToken);
    }

    private async Task CreateIntentPinIndexesAsync(CancellationToken cancellationToken)
    {
        var pins = database.GetCollection<IntentPinDocument>(MongoCollectionNames.IntentPins);
        await pins.Indexes.CreateManyAsync(
            [
                // One pin per (owner, intent, context). Race-protects the upsert path: even
                // if two PinAsync calls land at the same time, only one document survives.
                new CreateIndexModel<IntentPinDocument>(
                    Builders<IntentPinDocument>.IndexKeys
                        .Ascending(x => x.OwnerUserId)
                        .Ascending(x => x.IntentId)
                        .Ascending(x => x.ContextTagId),
                    new CreateIndexOptions { Unique = true, Name = "owner_intent_context_unique" }),
                // Drives the per-context ordered listing (sidebar Pinned section).
                new CreateIndexModel<IntentPinDocument>(
                    Builders<IntentPinDocument>.IndexKeys
                        .Ascending(x => x.OwnerUserId)
                        .Ascending(x => x.ContextTagId)
                        .Ascending(x => x.PinSortKey),
                    new CreateIndexOptions { Name = "owner_context_sort_key" }),
                // Batch lookup of pinned_in for the list DTOs.
                new CreateIndexModel<IntentPinDocument>(
                    Builders<IntentPinDocument>.IndexKeys
                        .Ascending(x => x.OwnerUserId)
                        .Ascending(x => x.IntentId),
                    new CreateIndexOptions { Name = "owner_intent" }),
            ],
            cancellationToken);
    }

    private async Task CreateIntentLinkIndexesAsync(CancellationToken cancellationToken)
    {
        var intentLinks = database.GetCollection<IntentLinkDocument>(MongoCollectionNames.IntentLinks);
        await intentLinks.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<IntentLinkDocument>(
                    Builders<IntentLinkDocument>.IndexKeys
                        .Ascending(x => x.FromId)
                        .Ascending(x => x.ToId)
                        .Ascending(x => x.Type),
                    new CreateIndexOptions { Unique = true, Name = "from_to_type_unique" }),
                new CreateIndexModel<IntentLinkDocument>(
                    Builders<IntentLinkDocument>.IndexKeys.Ascending(x => x.FromId),
                    new CreateIndexOptions { Name = "from_id" }),
                new CreateIndexModel<IntentLinkDocument>(
                    Builders<IntentLinkDocument>.IndexKeys.Ascending(x => x.ToId),
                    new CreateIndexOptions { Name = "to_id" }),
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
