using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Throne.Infrastructure.Mongo.Documents;
using Throne.Infrastructure.Mongo.Repositories;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoIndexInitializer(IMongoDatabase database) : BackgroundService
{
    // Runs off the startup critical path: index creation is idempotent (the driver
    // no-ops indexes that already exist), so the host can start listening before it
    // finishes instead of blocking "Application started" on a round-trip per index.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        ExecuteWhenPrimaryAsync(CreateIndexesAsync, stoppingToken);

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
        await intentAttachments.Indexes.CreateOneAsync(
            new CreateIndexModel<IntentAttachmentDocument>(
                Builders<IntentAttachmentDocument>.IndexKeys.Ascending(x => x.IntentId),
                new CreateIndexOptions { Name = "intent_id" }),
            cancellationToken: cancellationToken);

        var tags = database.GetCollection<TagDocument>(MongoCollectionNames.Tags);
        await tags.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<TagDocument>(
                    Builders<TagDocument>.IndexKeys.Ascending(x => x.Name),
                    new CreateIndexOptions { Unique = true, Name = "name_unique" }),
                // Powers the board's `usage_count desc, _id asc` keyset pagination.
                new CreateIndexModel<TagDocument>(
                    Builders<TagDocument>.IndexKeys
                        .Descending(x => x.UsageCount)
                        .Ascending(x => x.Id),
                    new CreateIndexOptions { Name = "usage_count_id" }),
            ],
            cancellationToken);

        var promptParts = database.GetCollection<PromptPartDocument>(MongoCollectionNames.PromptParts);
        await promptParts.Indexes.CreateOneAsync(
            new CreateIndexModel<PromptPartDocument>(
                Builders<PromptPartDocument>.IndexKeys
                    .Ascending(x => x.Scope)
                    .Ascending(x => x.Key),
                new CreateIndexOptions { Unique = true, Name = "scope_key_unique" }),
            cancellationToken: cancellationToken);

        var skillModeDefaults = database.GetCollection<SkillModeDefaultDocument>(
            MongoCollectionNames.SkillModeDefaults);
        await skillModeDefaults.Indexes.CreateOneAsync(
            new CreateIndexModel<SkillModeDefaultDocument>(
                Builders<SkillModeDefaultDocument>.IndexKeys
                    .Ascending(x => x.Mode)
                    .Ascending(x => x.SkillId),
                new CreateIndexOptions { Unique = true, Name = "mode_skill_unique" }),
            cancellationToken: cancellationToken);

        var intentsCollection = database.GetCollection<IntentDocument>(MongoCollectionNames.Intents);
        await intentsCollection.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<IntentDocument>(
                    Builders<IntentDocument>.IndexKeys.Ascending(x => x.TagIds),
                    new CreateIndexOptions { Name = "tag_ids" }),
                new CreateIndexModel<IntentDocument>(
                    Builders<IntentDocument>.IndexKeys.Ascending(x => x.SortKey),
                    new CreateIndexOptions { Name = "sort_key" }),
                // Powers sort=updated_desc with `(updated_at desc, id asc)` keyset
                // pagination (list-page performance at 100k scale).
                new CreateIndexModel<IntentDocument>(
                    Builders<IntentDocument>.IndexKeys
                        .Descending(x => x.UpdatedAt)
                        .Ascending(x => x.Id),
                    new CreateIndexOptions { Name = "updated_at_id" }),
                // Powers sort=created_desc / created_asc.
                new CreateIndexModel<IntentDocument>(
                    Builders<IntentDocument>.IndexKeys
                        .Ascending(x => x.CreatedAt)
                        .Ascending(x => x.Id),
                    new CreateIndexOptions { Name = "created_at_id" }),
            ],
            cancellationToken);

        await MongoPromptPartPatchIndexes.CreateAsync(database, cancellationToken);
        await MongoDreamSessionIndexes.CreateAsync(database, cancellationToken);

        await MongoIntentLinkMigration.RunAsync(database, cancellationToken);
        await MongoTagUsageBackfill.RunAsync(database, cancellationToken);
        await CreateIntentLinkIndexesAsync(cancellationToken);
        await CreateIntentEventIndexesAsync(cancellationToken);
        await CreateIntentPinIndexesAsync(cancellationToken);
        await MongoIntentRepositoryBindingIndexes.CreateAsync(database, cancellationToken);
        await MongoRepositoryIndexes.CreateAsync(database, cancellationToken);
        await MongoPullRequestArtifactIndexes.CreateAsync(database, cancellationToken);
    }

    private async Task CreateIntentEventIndexesAsync(CancellationToken cancellationToken)
    {
        var intentEvents = database.GetCollection<IntentEventDocument>(MongoCollectionNames.IntentEvents);
        await intentEvents.Indexes.CreateManyAsync(
            [
                // Primary lookup for the per-intent feed.
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
                // One text_changed event per (intent_id, version): the unique partial
                // index guards the write path against accidental double-writes of the
                // same version.
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
                // One pin per (intent, context). Race-protects the upsert path: even
                // if two PinAsync calls land at the same time, only one document survives.
                new CreateIndexModel<IntentPinDocument>(
                    Builders<IntentPinDocument>.IndexKeys
                        .Ascending(x => x.IntentId)
                        .Ascending(x => x.ContextTagId),
                    new CreateIndexOptions { Unique = true, Name = "intent_context_unique" }),
                // Drives the per-context ordered listing (sidebar Pinned section).
                new CreateIndexModel<IntentPinDocument>(
                    Builders<IntentPinDocument>.IndexKeys
                        .Ascending(x => x.ContextTagId)
                        .Ascending(x => x.PinSortKey),
                    new CreateIndexOptions { Name = "context_sort_key" }),
                // Batch lookup of pinned_in for the list DTOs.
                new CreateIndexModel<IntentPinDocument>(
                    Builders<IntentPinDocument>.IndexKeys
                        .Ascending(x => x.IntentId),
                    new CreateIndexOptions { Name = "intent" }),
            ],
            cancellationToken);
    }

    private async Task CreateIntentLinkIndexesAsync(CancellationToken cancellationToken)
    {
        var intentLinks = database.GetCollection<IntentLinkDocument>(MongoCollectionNames.IntentLinks);
        await MongoIntentLinkMigration.DropOldUniqueIndexAsync(database, cancellationToken);
        await intentLinks.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<IntentLinkDocument>(
                    Builders<IntentLinkDocument>.IndexKeys
                        .Ascending(x => x.FromId)
                        .Ascending(x => x.ToId),
                    new CreateIndexOptions { Unique = true, Name = "from_to_unique" }),
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
