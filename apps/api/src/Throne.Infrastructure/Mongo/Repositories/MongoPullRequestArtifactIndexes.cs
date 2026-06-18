using MongoDB.Driver;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Repositories;

internal static class MongoPullRequestArtifactIndexes
{
    public static async Task CreateAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var artifacts = database.GetCollection<PullRequestArtifactDocument>(
            MongoCollectionNames.PullRequestArtifacts);

        await artifacts.Indexes.CreateManyAsync(
            [
                new CreateIndexModel<PullRequestArtifactDocument>(
                    Builders<PullRequestArtifactDocument>.IndexKeys
                        .Ascending(x => x.BindingId)
                        .Ascending(x => x.Type),
                    new CreateIndexOptions { Unique = true, Name = "binding_type_unique" }),
                new CreateIndexModel<PullRequestArtifactDocument>(
                    Builders<PullRequestArtifactDocument>.IndexKeys.Ascending(x => x.BindingId),
                    new CreateIndexOptions { Name = "binding_id" }),
            ],
            cancellationToken);
    }
}
