using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Repositories;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.Tests.Mongo.Repositories;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public class MongoRepositoryRegistryTests(SqliteFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 6, 6, 12, 0, 0, TimeSpan.Zero);

    private static RepoCoordinate Coordinate(string repo = "throne") =>
        new(GitProviderNames.GitHub, "octo", repo);

    [Fact(DisplayName = "EnsureRepositoryAsync вставляет строку реестра в коллекцию repositories")]
    public async Task Ensure_inserts_row()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);

        var outcome = await EnsureAsync(scope, Coordinate(), Now);

        outcome.Should().BeOfType<EnsureRepositoryOutcome.Created>();
        var repository = outcome.Repository;
        repository.Coordinate.Should().Be(Coordinate());
        var stored = await FindRepositoryAsync(scope.Database, repository.Id.Value);
        stored.Should().NotBeNull();
        stored!.Provider.Should().Be(GitProviderNames.GitHub);
        stored.Owner.Should().Be("octo");
        stored.Repo.Should().Be("throne");
    }

    [Fact(DisplayName = "EnsureRepositoryAsync идемпотентен: вторая запись той же координаты не плодит строк")]
    public async Task Ensure_is_idempotent()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);

        var first = await EnsureAsync(scope, Coordinate(), Now);
        var second = await EnsureAsync(scope, Coordinate(), Now.AddMinutes(1));

        first.Should().BeOfType<EnsureRepositoryOutcome.Created>();
        second.Should().BeOfType<EnsureRepositoryOutcome.Existed>();
        second.Repository.Id.Should().Be(first.Repository.Id);
        await using var ctx = await scope.Database.CreateContextAsync();
        var count = await ctx.Set<RepositoryRow>().AsNoTracking().CountAsync(CancellationToken.None);
        count.Should().Be(1);
    }

    [Fact(DisplayName = "EnsureRepositoryAsync различает разные координаты")]
    public async Task Ensure_distinct_coordinates()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);

        var a = await EnsureAsync(scope, Coordinate("alpha"), Now);
        var b = await EnsureAsync(scope, Coordinate("beta"), Now);

        b.Repository.Id.Should().NotBe(a.Repository.Id);
    }

    [Fact(DisplayName = "Уникальный индекс по координате запрещает дубликат строки реестра")]
    public async Task Coordinate_index_is_unique()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);
        await EnsureAsync(scope, Coordinate(), Now);

        var act = async () =>
        {
            await using var db = await scope.Database.CreateContextAsync();
            db.Set<RepositoryRow>().Add(new RepositoryRow
            {
                Id = Guid.NewGuid().ToString("N"),
                Provider = GitProviderNames.GitHub,
                Host = GitProviderHostDefaults.GitHub,
                Owner = "octo",
                Repo = "throne",
                CreatedAt = Now,
                UpdatedAt = Now,
            });
            await db.SaveChangesAsync(CancellationToken.None);
        };

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact(DisplayName = "FindByCoordinateAsync возвращает null до первого появления координаты")]
    public async Task Find_returns_null_when_absent()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);

        var found = await scope.Registry.FindByCoordinateAsync(Coordinate(), CancellationToken.None);

        found.Should().BeNull();
    }

    [Fact(DisplayName = "ListAsync пуст до первой регистрации")]
    public async Task List_is_empty_initially()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);

        var all = await scope.Registry.ListAsync(CancellationToken.None);

        all.Should().BeEmpty();
    }

    [Fact(DisplayName = "ListAsync возвращает все зарегистрированные репо, отсортированные по координате")]
    public async Task List_returns_all_sorted()
    {
        var scope = await RepositoryStoreTestScope.CreateAsync(fixture);
        await EnsureAsync(scope, Coordinate("zeta"), Now);
        await EnsureAsync(scope, Coordinate("alpha"), Now);

        var all = await scope.Registry.ListAsync(CancellationToken.None);

        all.Select(r => r.Coordinate.Repo).Should().Equal("alpha", "zeta");
    }

    private static async Task<RepositoryRow?> FindRepositoryAsync(SqliteTestDatabase database, string id)
    {
        await using var ctx = await database.CreateContextAsync();
        return await ctx.Set<RepositoryRow>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
    }

    private static Task<EnsureRepositoryOutcome> EnsureAsync(
        RepositoryStoreTestScope scope,
        RepoCoordinate coordinate,
        DateTimeOffset now) =>
        scope.UnitOfWork.ExecuteAsync(
            ct => scope.Registry.EnsureRepositoryAsync(coordinate, now, ct),
            CancellationToken.None);
}
