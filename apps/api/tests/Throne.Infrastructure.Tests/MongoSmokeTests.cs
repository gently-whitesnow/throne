using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace Throne.Infrastructure.Tests;

public class MongoSmokeTests : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder().Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task MongoDB_responds_to_ping()
    {
        var client = new MongoClient(_container.GetConnectionString());
        var db = client.GetDatabase("throne_smoke");

        var result = await db.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

        result["ok"].ToDouble().Should().Be(1.0);
    }
}
