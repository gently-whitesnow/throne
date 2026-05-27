using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Infrastructure.Mongo;

namespace Throne.Infrastructure.Tests.Mongo.Capabilities;

internal sealed record CapabilitiesRepositoryTestScope(
    IMongoDatabase Database,
    ICapabilitiesRepository Repository,
    IUnitOfWork Uow)
{
    public static async Task<CapabilitiesRepositoryTestScope> CreateAsync(MongoFixture fixture)
    {
        var name = $"throne_test_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(name);
        var db = fixture.Client.GetDatabase(name);

        var sessions = new MongoSessionAccessor();
        var repo = new MongoCapabilitiesRepository(db, sessions);
        var uow = new MongoUnitOfWork(fixture.Client, sessions);
        return new CapabilitiesRepositoryTestScope(db, repo, uow);
    }
}
