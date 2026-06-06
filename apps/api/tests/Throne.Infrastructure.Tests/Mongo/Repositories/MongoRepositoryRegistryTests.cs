using FluentAssertions;
using MongoDB.Driver;
using Throne.Domain.Repositories;
using Throne.Infrastructure.Mongo;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Tests.Mongo.Repositories;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public class MongoRepositoryRegistryTests(MongoFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 6, 6, 12, 0, 0, TimeSpan.Zero);

    private static RepoCoordinate Coordinate(string repo = "throne") =>
        new(GitProviderNames.GitHub, "octo", repo);

    [Fact(DisplayName = "EnsureRepositoryAsync вставляет строку реестра в коллекцию repositories")]
    public async Task Ensure_inserts_row()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);

        var repository = await scope.Registry.EnsureRepositoryAsync(Coordinate(), Now, CancellationToken.None);

        repository.Coordinate.Should().Be(Coordinate());
        var stored = await scope.Database
            .GetCollection<RepositoryDocument>(MongoCollectionNames.Repositories)
            .Find(d => d.Id == repository.Id.Value)
            .FirstOrDefaultAsync();
        stored.Should().NotBeNull();
        stored!.Provider.Should().Be(GitProviderNames.GitHub);
        stored.Owner.Should().Be("octo");
        stored.Repo.Should().Be("throne");
    }

    [Fact(DisplayName = "EnsureRepositoryAsync идемпотентен: вторая запись той же координаты не плодит строк")]
    public async Task Ensure_is_idempotent()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);

        var first = await scope.Registry.EnsureRepositoryAsync(Coordinate(), Now, CancellationToken.None);
        var second = await scope.Registry.EnsureRepositoryAsync(
            Coordinate(), Now.AddMinutes(1), CancellationToken.None);

        second.Id.Should().Be(first.Id);
        var count = await scope.Database
            .GetCollection<RepositoryDocument>(MongoCollectionNames.Repositories)
            .CountDocumentsAsync(FilterDefinition<RepositoryDocument>.Empty);
        count.Should().Be(1);
    }

    [Fact(DisplayName = "EnsureRepositoryAsync различает разные координаты")]
    public async Task Ensure_distinct_coordinates()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);

        var a = await scope.Registry.EnsureRepositoryAsync(Coordinate("alpha"), Now, CancellationToken.None);
        var b = await scope.Registry.EnsureRepositoryAsync(Coordinate("beta"), Now, CancellationToken.None);

        b.Id.Should().NotBe(a.Id);
    }

    [Fact(DisplayName = "Уникальный индекс по координате запрещает дубликат строки реестра")]
    public async Task Coordinate_index_is_unique()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);
        var collection = scope.Database.GetCollection<RepositoryDocument>(MongoCollectionNames.Repositories);
        await scope.Registry.EnsureRepositoryAsync(Coordinate(), Now, CancellationToken.None);

        var act = async () => await collection.InsertOneAsync(new RepositoryDocument
        {
            Id = Guid.NewGuid().ToString("N"),
            Provider = GitProviderNames.GitHub,
            Owner = "octo",
            Repo = "throne",
            CreatedAt = Now.UtcDateTime,
            UpdatedAt = Now.UtcDateTime,
        });

        await act.Should().ThrowAsync<MongoWriteException>();
    }

    [Fact(DisplayName = "FindByCoordinateAsync возвращает null до первого появления координаты")]
    public async Task Find_returns_null_when_absent()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);

        var found = await scope.Registry.FindByCoordinateAsync(Coordinate(), CancellationToken.None);

        found.Should().BeNull();
    }
}
