using FluentAssertions;
using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Repositories;
using Throne.Infrastructure.Mongo;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Tests.Mongo.Repositories;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public class MongoRepositoryArtifactStoreTests(MongoFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 6, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly RepoCoordinate Coordinate = new(GitProviderNames.GitHub, "octo", "throne");

    private static Task<WriteRepositoryArtifactOutcome> Write(
        RepositoryStoreTestScope scope,
        string title,
        string document,
        int? expectedVersion,
        string slug = "db-schema-map",
        DateTimeOffset? at = null) =>
        scope.Artifacts.WriteAsync(
            Coordinate, slug, title, document, expectedVersion, at ?? Now, CancellationToken.None);

    [Fact(DisplayName = "Первая запись создаёт артефакт version=1 и снапшот в истории")]
    public async Task First_write_creates_version_one()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);

        var outcome = await Write(scope, "DB schema", "# erd", expectedVersion: null);

        var written = outcome.Should().BeOfType<WriteRepositoryArtifactOutcome.Written>().Subject;
        written.Created.Should().BeTrue();
        written.Artifact.Version.Should().Be(1);

        var versions = await scope.Artifacts.ListVersionsAsync(written.Artifact.Id, CancellationToken.None);
        versions.Should().HaveCount(1);
        versions[0].Version.Should().Be(1);
        versions[0].Document.Should().Be("# erd");
    }

    [Fact(DisplayName = "Запись с expected_version == current апдейтит и пишет новый снапшот")]
    public async Task Update_with_matching_expected_version()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);
        var created = (WriteRepositoryArtifactOutcome.Written)await Write(scope, "v1", "body1", expectedVersion: null);

        var outcome = await Write(
            scope, "v2", "body2", expectedVersion: 1, at: Now.AddMinutes(1));

        var written = outcome.Should().BeOfType<WriteRepositoryArtifactOutcome.Written>().Subject;
        written.Created.Should().BeFalse();
        written.Artifact.Version.Should().Be(2);
        written.Artifact.Title.Should().Be("v2");

        var fresh = await scope.Artifacts.FindBySlugAsync(Coordinate, "db-schema-map", CancellationToken.None);
        fresh!.Version.Should().Be(2);
        fresh.Document.Should().Be("body2");

        var versions = await scope.Artifacts.ListVersionsAsync(created.Artifact.Id, CancellationToken.None);
        versions.Select(v => v.Version).Should().Equal(1, 2);
    }

    [Fact(DisplayName = "Запись с неверным expected_version возвращает VersionConflict и не плодит снапшот")]
    public async Task Update_with_stale_expected_version_conflicts()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);
        var created = (WriteRepositoryArtifactOutcome.Written)await Write(scope, "v1", "body1", expectedVersion: null);

        var outcome = await Write(scope, "v2", "body2", expectedVersion: 5);

        outcome.Should().BeOfType<WriteRepositoryArtifactOutcome.VersionConflict>()
            .Subject.CurrentVersion.Should().Be(1);

        var versions = await scope.Artifacts.ListVersionsAsync(created.Artifact.Id, CancellationToken.None);
        versions.Should().HaveCount(1);
        var fresh = await scope.Artifacts.FindBySlugAsync(Coordinate, "db-schema-map", CancellationToken.None);
        fresh!.Title.Should().Be("v1");
    }

    [Fact(DisplayName = "expected_version на несуществующей странице возвращает VersionConflict(0)")]
    public async Task Expected_version_on_missing_page_conflicts()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);

        var outcome = await Write(scope, "v1", "body", expectedVersion: 3);

        outcome.Should().BeOfType<WriteRepositoryArtifactOutcome.VersionConflict>()
            .Subject.CurrentVersion.Should().Be(0);
        (await scope.Artifacts.FindBySlugAsync(Coordinate, "db-schema-map", CancellationToken.None))
            .Should().BeNull();
    }

    [Fact(DisplayName = "ListByCoordinateAsync возвращает страницы репо, отсортированные по slug")]
    public async Task List_by_coordinate_sorted_by_slug()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);
        await Write(scope, "Map", "m", expectedVersion: null, slug: "db-schema-map");
        await Write(scope, "Arch", "a", expectedVersion: null, slug: "architecture-overview");

        var pages = await scope.Artifacts.ListByCoordinateAsync(Coordinate, CancellationToken.None);

        pages.Select(p => p.Slug).Should().Equal("architecture-overview", "db-schema-map");
    }

    [Fact(DisplayName = "Уникальный индекс (coordinate, slug) запрещает второй документ той же страницы")]
    public async Task Coordinate_slug_index_is_unique()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);
        await Write(scope, "v1", "body", expectedVersion: null);
        var collection = scope.Database.GetCollection<RepositoryArtifactDocument>(
            MongoCollectionNames.RepositoryArtifacts);

        var act = async () => await collection.InsertOneAsync(new RepositoryArtifactDocument
        {
            Id = Guid.NewGuid().ToString("N"),
            Provider = GitProviderNames.GitHub,
            Owner = "octo",
            Repo = "throne",
            Slug = "db-schema-map",
            Title = "dup",
            Document = "dup",
            Version = 1,
            CreatedAt = Now.UtcDateTime,
            UpdatedAt = Now.UtcDateTime,
        });

        await act.Should().ThrowAsync<MongoWriteException>();
    }

    [Fact(DisplayName = "Уникальный индекс (artifact_id, version) запрещает дубль снапшота версии")]
    public async Task Artifact_version_index_is_unique()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);
        var created = (WriteRepositoryArtifactOutcome.Written)await Write(scope, "v1", "body", expectedVersion: null);
        var collection = scope.Database.GetCollection<RepositoryArtifactVersionDocument>(
            MongoCollectionNames.RepositoryArtifactVersions);

        var act = async () => await collection.InsertOneAsync(new RepositoryArtifactVersionDocument
        {
            Id = Guid.NewGuid().ToString("N"),
            ArtifactId = created.Artifact.Id.Value,
            Version = 1,
            Title = "dup",
            Document = "dup",
            CreatedAt = Now.UtcDateTime,
        });

        await act.Should().ThrowAsync<MongoWriteException>();
    }
}
