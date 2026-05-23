using MongoDB.Driver;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Repositories;

internal static class MongoPullRequestCommentIndexes
{
    /// <summary>
    /// Indexes for the <c>pull_request_comments</c> collection (T-10, ADR-0024 § 6):
    /// <list type="bullet">
    ///   <item>
    ///     Unique <c>(binding_id, upstream_id)</c> — the «poll is idempotent» invariant
    ///     of <see cref="MongoPullRequestCommentStore.PersistNewAsync"/>. Without it a
    ///     concurrent manual sync racing the background poller could duplicate fanout.
    ///   </item>
    ///   <item>
    ///     Composite <c>(binding_id, created_at)</c> — drives the per-binding listing
    ///     consumed by the HTTP / MCP read paths (T-11 / T-13).
    ///   </item>
    /// </list>
    /// </summary>
    public static async Task CreateAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<PullRequestCommentDocument>(
            MongoCollectionNames.PullRequestComments);

        await collection.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<PullRequestCommentDocument>(
                    Builders<PullRequestCommentDocument>.IndexKeys
                        .Ascending(x => x.BindingId)
                        .Ascending(x => x.UpstreamId),
                    new CreateIndexOptions { Unique = true, Name = "binding_upstream_unique" }),
                new CreateIndexModel<PullRequestCommentDocument>(
                    Builders<PullRequestCommentDocument>.IndexKeys
                        .Ascending(x => x.BindingId)
                        .Descending(x => x.CreatedAt),
                    new CreateIndexOptions { Name = "binding_created_at" }),
            ],
            cancellationToken);
    }
}
