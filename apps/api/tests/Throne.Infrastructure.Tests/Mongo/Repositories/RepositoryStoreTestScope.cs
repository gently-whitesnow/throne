using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Infrastructure.Mongo;
using Throne.Infrastructure.Mongo.Repositories;

namespace Throne.Infrastructure.Tests.Mongo.Repositories;

/// <summary>
/// Per-test scope: fresh database + the registry / artifact stores wired against the same
/// indexes the production <c>MongoIndexInitializer</c> builds, so the unique-index drift
/// guards run against the real schema.
/// </summary>
internal sealed record RepositoryStoreTestScope(
    IMongoDatabase Database,
    IRepositoryRegistry Registry,
    IRepositoryArtifactRepository Artifacts,
    IUnitOfWork UnitOfWork)
{
    public static async Task<RepositoryStoreTestScope> CreateAsync(MongoFixture fixture)
    {
        var name = $"throne_test_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(name);
        var db = fixture.Client.GetDatabase(name);

        await MongoRepositoryIndexes.CreateAsync(db, CancellationToken.None);
        await MongoRepositoryArtifactIndexes.CreateAsync(db, CancellationToken.None);

        var sessions = new MongoSessionAccessor();
        var registry = new MongoRepositoryRegistry(db, sessions);
        var artifacts = new MongoRepositoryArtifactStore(db, sessions);
        var uow = new MongoUnitOfWork(fixture.Client, sessions);
        return new RepositoryStoreTestScope(db, registry, artifacts, uow);
    }
}
