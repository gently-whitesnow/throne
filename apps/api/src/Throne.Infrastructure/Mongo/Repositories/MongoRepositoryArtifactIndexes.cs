using MongoDB.Driver;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Repositories;

internal static class MongoRepositoryArtifactIndexes
{
    /// <summary>
    /// Indexes for the knowledge-page aggregate (ADR-0031):
    /// <list type="bullet">
    ///   <item>Unique <c>(provider, owner, repo, slug)</c> on <c>repository_artifacts</c> —
    ///         one page per slug per repository; closes the create race.</item>
    ///   <item>Unique <c>(artifact_id, version)</c> on <c>repository_artifact_versions</c> —
    ///         one snapshot per version in the append-only history.</item>
    /// </list>
    /// </summary>
    public static async Task CreateAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var artifacts = database.GetCollection<RepositoryArtifactDocument>(MongoCollectionNames.RepositoryArtifacts);
        await artifacts.Indexes.CreateOneAsync(
            new CreateIndexModel<RepositoryArtifactDocument>(
                Builders<RepositoryArtifactDocument>.IndexKeys
                    .Ascending(x => x.Provider)
                    .Ascending(x => x.Owner)
                    .Ascending(x => x.Repo)
                    .Ascending(x => x.Slug),
                new CreateIndexOptions { Unique = true, Name = "coordinate_slug_unique" }),
            cancellationToken: cancellationToken);

        var versions = database.GetCollection<RepositoryArtifactVersionDocument>(
            MongoCollectionNames.RepositoryArtifactVersions);
        await versions.Indexes.CreateOneAsync(
            new CreateIndexModel<RepositoryArtifactVersionDocument>(
                Builders<RepositoryArtifactVersionDocument>.IndexKeys
                    .Ascending(x => x.ArtifactId)
                    .Ascending(x => x.Version),
                new CreateIndexOptions { Unique = true, Name = "artifact_version_unique" }),
            cancellationToken: cancellationToken);
    }
}
