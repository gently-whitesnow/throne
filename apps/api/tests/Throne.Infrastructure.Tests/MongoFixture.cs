using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace Throne.Infrastructure.Tests;

public sealed class MongoFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder().Build();
    public IMongoDatabase Database { get; private set; } = null!;
    public IMongoClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Client = new MongoClient(_container.GetConnectionString());
        Database = Client.GetDatabase("throne_test");
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
