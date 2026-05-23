using MongoDB.Driver;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Repositories;

internal static class MongoIntentRepositoryBindingIndexes
{
    /// <summary>
    /// Indexes for <c>intent_repository_bindings</c> per T-04 / ADR-0024:
    /// <list type="bullet">
    ///   <item>
    ///     Unique <c>(intent_id, provider, owner, repo)</c> — enforces «one bind per
    ///     coordinate per intent» so the duplicate-bind path returns 409 even if two
    ///     requests race.
    ///   </item>
    ///   <item>
    ///     Secondary <c>{intent_id}</c> — drives the per-intent listing (UI / MCP).
    ///   </item>
    ///   <item>
    ///     Secondary <c>(pull_request_state, last_synced_at)</c> — drives
    ///     <c>FindOpenForSync</c>: filter by <c>state == open</c>, then walk ascending by
    ///     <c>last_synced_at</c>. Replaces the earlier <c>(pull_request_number,
    ///     clone_status)</c> composite per the 2026-05-23 review note.
    ///   </item>
    /// </list>
    /// </summary>
    public static async Task CreateAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<IntentRepositoryBindingDocument>(
            MongoCollectionNames.IntentRepositoryBindings);

        await collection.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<IntentRepositoryBindingDocument>(
                    Builders<IntentRepositoryBindingDocument>.IndexKeys
                        .Ascending(x => x.IntentId)
                        .Ascending(x => x.Provider)
                        .Ascending(x => x.Owner)
                        .Ascending(x => x.Repo),
                    new CreateIndexOptions { Unique = true, Name = "intent_coordinate_unique" }),
                new CreateIndexModel<IntentRepositoryBindingDocument>(
                    Builders<IntentRepositoryBindingDocument>.IndexKeys.Ascending(x => x.IntentId),
                    new CreateIndexOptions { Name = "intent_id" }),
                new CreateIndexModel<IntentRepositoryBindingDocument>(
                    Builders<IntentRepositoryBindingDocument>.IndexKeys
                        .Ascending(x => x.PullRequestState)
                        .Ascending(x => x.LastSyncedAt),
                    new CreateIndexOptions { Name = "pr_state_last_synced_at" }),
            ],
            cancellationToken);
    }
}
