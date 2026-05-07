using Testcontainers.MongoDb;

namespace Throne.Api.Tests.Infrastructure;

/// <summary>
/// Shared MongoDB replica-set container for the whole Throne.Api.Tests run.
/// Exposed as <see cref="ICollectionFixture{TFixture}"/> through
/// <see cref="MongoIntegrationFixture"/>: all integration tests in
/// Throne.Api.Tests share a single container and isolate themselves with a
/// per-test database name. This keeps replica-set initialization sequential
/// (replica-set bootstrap is what flakes under parallel docker exec).
/// </summary>
public sealed class MongoFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder().WithReplicaSet().Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var raw = _container.GetConnectionString();
        var separator = raw.Contains('?') ? '&' : '?';
        ConnectionString = $"{raw}{separator}directConnection=true";
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
