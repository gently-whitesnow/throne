using FluentAssertions;
using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Repositories;
using Throne.Domain.Tags;
using Throne.Infrastructure.Mongo;
using Throne.Infrastructure.Mongo.Documents;
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Infrastructure.Tests.Mongo.Tags;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public class MongoTagDefaultRepositoriesTests(MongoFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "SetDefaultRepositoriesAsync пишет subdocument'ы и поднимает версию")]
    public async Task SetDefaultRepositories_persists_subdocuments_and_bumps_version()
    {
        var scope = await TagRepositoryTestScope.CreateAsync(fixture);
        var tag = await EnsureTagAsync(scope, "throne");

        var defaults = new[]
        {
            Default("anthropics", "throne"),
            Default("anthropics", "claude-code", "develop"),
        };

        var outcome = await scope.Uow.ExecuteAsync(
            ct => scope.Repository.SetDefaultRepositoriesAsync(
                tag.Id, tag.CurrentVersion, defaults, Now.AddMinutes(1), ct),
            CancellationToken.None);

        outcome.Should().BeOfType<SetTagDefaultRepositoriesOutcome.Updated>();
        var updated = ((SetTagDefaultRepositoriesOutcome.Updated)outcome).Tag;
        updated.DefaultRepositories.Should().HaveCount(2);
        updated.CurrentVersion.Should().Be(tag.CurrentVersion + 1);

        var stored = await scope.Database
            .GetCollection<TagDocument>(MongoCollectionNames.Tags)
            .Find(d => d.Id == tag.Id.Value)
            .FirstOrDefaultAsync();
        stored.Should().NotBeNull();
        stored!.DefaultRepositories.Should().HaveCount(2);
        stored.DefaultRepositories[0].Owner.Should().Be("anthropics");
        stored.DefaultRepositories[0].Repo.Should().Be("throne");
        stored.DefaultRepositories[1].Repo.Should().Be("claude-code");
        stored.DefaultRepositories[1].DefaultBranch.Should().Be("develop");
    }

    [Fact(DisplayName = "GetByIdAsync читает default_repositories в доменной форме")]
    public async Task Get_returns_persisted_default_repositories()
    {
        var scope = await TagRepositoryTestScope.CreateAsync(fixture);
        var tag = await EnsureTagAsync(scope, "throne");
        await scope.Uow.ExecuteAsync(
            ct => scope.Repository.SetDefaultRepositoriesAsync(
                tag.Id, tag.CurrentVersion, [Default("anthropics", "throne")], Now, ct),
            CancellationToken.None);

        var fetched = await scope.Repository.GetByIdAsync(tag.Id, CancellationToken.None);

        fetched.Should().NotBeNull();
        fetched!.DefaultRepositories.Should().ContainSingle();
        var entry = fetched.DefaultRepositories[0];
        entry.Coordinate.Provider.Should().Be(GitProviderNames.GitHub);
        entry.Coordinate.Owner.Should().Be("anthropics");
        entry.Coordinate.Repo.Should().Be("throne");
        entry.DefaultBranch.Should().BeNull();
    }

    [Fact(DisplayName = "SetDefaultRepositoriesAsync пустым массивом очищает persisted subdocument'ы")]
    public async Task SetDefaultRepositories_with_empty_clears_persisted_list()
    {
        var scope = await TagRepositoryTestScope.CreateAsync(fixture);
        var tag = await EnsureTagAsync(scope, "throne");
        await scope.Uow.ExecuteAsync(
            ct => scope.Repository.SetDefaultRepositoriesAsync(
                tag.Id, tag.CurrentVersion, [Default("anthropics", "throne")], Now, ct),
            CancellationToken.None);
        var afterFirst = await scope.Repository.GetByIdAsync(tag.Id, CancellationToken.None);

        var outcome = await scope.Uow.ExecuteAsync(
            ct => scope.Repository.SetDefaultRepositoriesAsync(
                tag.Id, afterFirst!.CurrentVersion, [], Now.AddMinutes(2), ct),
            CancellationToken.None);

        outcome.Should().BeOfType<SetTagDefaultRepositoriesOutcome.Updated>();
        var stored = await scope.Repository.GetByIdAsync(tag.Id, CancellationToken.None);
        stored!.DefaultRepositories.Should().BeEmpty();
    }

    [Fact(DisplayName = "SetDefaultRepositoriesAsync с устаревшим expected_version возвращает VersionConflict")]
    public async Task SetDefaultRepositories_returns_version_conflict_on_stale_expected_version()
    {
        var scope = await TagRepositoryTestScope.CreateAsync(fixture);
        var tag = await EnsureTagAsync(scope, "throne");

        var outcome = await scope.Uow.ExecuteAsync(
            ct => scope.Repository.SetDefaultRepositoriesAsync(
                tag.Id, expectedVersion: tag.CurrentVersion + 99, [Default("a", "b")], Now, ct),
            CancellationToken.None);

        outcome.Should().BeOfType<SetTagDefaultRepositoriesOutcome.VersionConflict>();
    }

    [Fact(DisplayName = "SetDefaultRepositoriesAsync на несуществующий тег возвращает NotFound")]
    public async Task SetDefaultRepositories_returns_not_found_for_missing_tag()
    {
        var scope = await TagRepositoryTestScope.CreateAsync(fixture);

        var outcome = await scope.Uow.ExecuteAsync(
            ct => scope.Repository.SetDefaultRepositoriesAsync(
                TagId.New(), expectedVersion: 1, [Default("a", "b")], Now, ct),
            CancellationToken.None);

        outcome.Should().BeOfType<SetTagDefaultRepositoriesOutcome.NotFound>();
    }

    [Fact(DisplayName = "SetDefaultRepositoriesAsync с тем же списком — NoChange (без version bump)")]
    public async Task SetDefaultRepositories_no_change_when_list_matches()
    {
        var scope = await TagRepositoryTestScope.CreateAsync(fixture);
        var tag = await EnsureTagAsync(scope, "throne");
        await scope.Uow.ExecuteAsync(
            ct => scope.Repository.SetDefaultRepositoriesAsync(
                tag.Id, tag.CurrentVersion, [Default("anthropics", "throne")], Now, ct),
            CancellationToken.None);
        var afterFirst = await scope.Repository.GetByIdAsync(tag.Id, CancellationToken.None);

        var outcome = await scope.Uow.ExecuteAsync(
            ct => scope.Repository.SetDefaultRepositoriesAsync(
                tag.Id, afterFirst!.CurrentVersion, [Default("anthropics", "throne")], Now.AddMinutes(5), ct),
            CancellationToken.None);

        outcome.Should().BeOfType<SetTagDefaultRepositoriesOutcome.NoChange>();
        var stored = await scope.Repository.GetByIdAsync(tag.Id, CancellationToken.None);
        stored!.CurrentVersion.Should().Be(afterFirst!.CurrentVersion);
    }

    private static async Task<Tag> EnsureTagAsync(TagRepositoryTestScope scope, string name)
    {
        var outcome = await scope.Repository.EnsureByNameAsync(name, Now, CancellationToken.None);
        return outcome switch
        {
            EnsureTagOutcome.Created c => c.Value,
            EnsureTagOutcome.Existed e => e.Value,
            _ => throw new InvalidOperationException("Unexpected EnsureTagOutcome shape."),
        };
    }

    private static TagDefaultRepository Default(string owner, string repo, string? branch = null) =>
        new(new RepoCoordinate(GitProviderNames.GitHub, owner, repo), branch);
}
