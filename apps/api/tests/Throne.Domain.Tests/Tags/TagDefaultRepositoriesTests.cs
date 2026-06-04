using FluentAssertions;
using Throne.Domain.Repositories;
using Throne.Domain.Tags;

namespace Throne.Domain.Tests.Tags;

public class TagDefaultRepositoriesTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Create стартует с пустым default_repositories")]
    public void Create_starts_with_empty_default_repositories()
    {
        var tag = Tag.Create(TagId.New(), "throne", Now);

        tag.DefaultRepositories.Should().BeEmpty();
    }

    [Fact(DisplayName = "ReplaceDefaultRepositories заменяет коллекцию и поднимает версию")]
    public void Replace_updates_collection_and_bumps_version()
    {
        var tag = Tag.Create(TagId.New(), "throne", Now);
        var initialVersion = tag.CurrentVersion;
        var later = Now.AddMinutes(5);

        var entry = Default("anthropics", "throne");
        var changed = tag.ReplaceDefaultRepositories([entry], later);

        changed.Should().BeTrue();
        tag.DefaultRepositories.Should().ContainSingle().Which.Should().BeEquivalentTo(entry);
        tag.CurrentVersion.Should().Be(initialVersion + 1);
        tag.UpdatedAt.Should().Be(later);
    }

    [Fact(DisplayName = "ReplaceDefaultRepositories на той же коллекции — no-op")]
    public void Replace_with_same_collection_is_no_op()
    {
        var tag = Tag.Create(TagId.New(), "throne", Now);
        tag.ReplaceDefaultRepositories([Default("anthropics", "throne")], Now.AddMinutes(1));
        var versionAfterFirst = tag.CurrentVersion;
        var updatedAfterFirst = tag.UpdatedAt;

        var changed = tag.ReplaceDefaultRepositories(
            [Default("anthropics", "throne")],
            Now.AddMinutes(10));

        changed.Should().BeFalse();
        tag.CurrentVersion.Should().Be(versionAfterFirst);
        tag.UpdatedAt.Should().Be(updatedAfterFirst);
    }

    [Fact(DisplayName = "ReplaceDefaultRepositories пустым массивом очищает коллекцию")]
    public void Replace_with_empty_clears_collection()
    {
        var tag = Tag.Create(TagId.New(), "throne", Now);
        tag.ReplaceDefaultRepositories([Default("anthropics", "throne")], Now);
        var versionBeforeClear = tag.CurrentVersion;

        var changed = tag.ReplaceDefaultRepositories([], Now.AddMinutes(1));

        changed.Should().BeTrue();
        tag.DefaultRepositories.Should().BeEmpty();
        tag.CurrentVersion.Should().Be(versionBeforeClear + 1);
    }

    [Fact(DisplayName = "ReplaceDefaultRepositories отвергает дубликаты по (provider, owner, repo)")]
    public void Replace_rejects_duplicates_on_coordinate()
    {
        var tag = Tag.Create(TagId.New(), "throne", Now);

        var act = () => tag.ReplaceDefaultRepositories(
            [
                Default("anthropics", "throne"),
                Default("anthropics", "throne", branch: "develop"),
            ],
            Now);

        act.Should().Throw<ArgumentException>().WithMessage("*unique*");
    }

    [Fact(DisplayName = "TagDefaultRepository отвергает default_branch из одних пробелов")]
    public void DefaultRepository_rejects_whitespace_branch()
    {
        var act = () => new TagDefaultRepository(
            new RepoCoordinate(GitProviderNames.GitHub, "anthropics", "throne"),
            DefaultBranch: "   ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Restore проверяет дубликаты в загружаемой коллекции")]
    public void Restore_rejects_persisted_duplicates()
    {
        var act = () => Tag.Restore(
            id: TagId.New(),
            name: "throne",
            currentVersion: 3,
            createdAt: Now,
            updatedAt: Now,
            defaultRepositories: [
                Default("anthropics", "throne"),
                Default("anthropics", "throne"),
            ]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Restore без default_repositories возвращает пустую коллекцию")]
    public void Restore_without_default_repositories_uses_empty()
    {
        var tag = Tag.Restore(
            id: TagId.New(),
            name: "throne",
            currentVersion: 1,
            createdAt: Now,
            updatedAt: Now);

        tag.DefaultRepositories.Should().BeEmpty();
    }

    private static TagDefaultRepository Default(string owner, string repo, string? branch = null) =>
        new(new RepoCoordinate(GitProviderNames.GitHub, owner, repo), branch);
}
