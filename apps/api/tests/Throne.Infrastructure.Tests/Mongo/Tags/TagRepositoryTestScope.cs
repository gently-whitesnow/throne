using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Infrastructure.Mongo;

namespace Throne.Infrastructure.Tests.Mongo.Tags;

internal sealed record TagRepositoryTestScope(
    IMongoDatabase Database,
    ITagRepository Repository,
    IUnitOfWork Uow)
{
    public static async Task<TagRepositoryTestScope> CreateAsync(MongoFixture fixture)
    {
        var name = $"throne_test_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(name);
        var db = fixture.Client.GetDatabase(name);

        var sessions = new MongoSessionAccessor();
        var repo = new MongoTagRepository(db, sessions);
        var uow = new MongoUnitOfWork(fixture.Client, sessions);
        return new TagRepositoryTestScope(db, repo, uow);
    }
}
