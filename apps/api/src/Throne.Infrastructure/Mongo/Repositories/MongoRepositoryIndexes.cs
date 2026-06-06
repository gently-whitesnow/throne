using MongoDB.Driver;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Repositories;

internal static class MongoRepositoryIndexes
{
    /// <summary>
    /// Unique <c>(provider, owner, repo)</c> on <c>repositories</c> (ADR-0031): the registry
    /// holds exactly one row per coordinate, and the unique index closes the
    /// first-appearance race in <c>EnsureRepositoryAsync</c>.
    /// </summary>
    public static async Task CreateAsync(IMongoDatabase database, CancellationToken cancellationToken)
    {
        var collection = database.GetCollection<RepositoryDocument>(MongoCollectionNames.Repositories);
        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<RepositoryDocument>(
                Builders<RepositoryDocument>.IndexKeys
                    .Ascending(x => x.Provider)
                    .Ascending(x => x.Owner)
                    .Ascending(x => x.Repo),
                new CreateIndexOptions { Unique = true, Name = "coordinate_unique" }),
            cancellationToken: cancellationToken);
    }
}
