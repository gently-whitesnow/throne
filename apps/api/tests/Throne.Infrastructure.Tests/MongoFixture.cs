using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace Throne.Infrastructure.Tests;

public sealed class MongoFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder().WithReplicaSet().Build();
    public IMongoDatabase Database { get; private set; } = null!;
    public IMongoClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var raw = _container.GetConnectionString();
        var separator = raw.Contains('?') ? '&' : '?';
        Client = new MongoClient($"{raw}{separator}directConnection=true");
        Database = Client.GetDatabase("throne_test");
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
