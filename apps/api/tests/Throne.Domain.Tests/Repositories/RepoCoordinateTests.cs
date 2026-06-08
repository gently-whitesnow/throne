using FluentAssertions;
using Throne.Domain.Repositories;

namespace Throne.Domain.Tests.Repositories;

public class RepoCoordinateTests
{
    [Fact(DisplayName = "GitHub: host по умолчанию нормализуется в github.com")]
    public void GitHub_host_defaults_to_github_com()
    {
        var coord = new RepoCoordinate(GitProviderNames.GitHub, "octocat", "hello-world");

        coord.Host.Should().Be(GitProviderHostDefaults.GitHub);
        coord.FullName.Should().Be("octocat/hello-world");
        coord.ProjectId.Should().BeNull();
    }

    [Fact(DisplayName = "GitHub: чужой host отклоняется (host зафиксирован на github.com)")]
    public void GitHub_rejects_foreign_host()
    {
        var act = () => new RepoCoordinate(GitProviderNames.GitHub, "octocat", "hello-world", Host: "ghe.corp");

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "GitHub: project_id запрещён")]
    public void GitHub_rejects_project_id()
    {
        var act = () => new RepoCoordinate(GitProviderNames.GitHub, "octocat", "hello-world", ProjectId: 42);

        act.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "GitHub: '__' и '.'/'..' в owner/repo отклоняются")]
    [InlineData("foo__bar", "repo")]
    [InlineData("owner", "..")]
    [InlineData("owner", ".")]
    public void GitHub_rejects_layout_and_traversal(string owner, string repo)
    {
        var act = () => new RepoCoordinate(GitProviderNames.GitHub, owner, repo);

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "GitLab: вложенный namespace group/subgroup принимается, host обязателен")]
    public void GitLab_accepts_nested_namespace()
    {
        var coord = new RepoCoordinate(
            GitProviderNames.GitLab, "group/subgroup", "service", Host: "gitlab.corp", ProjectId: 777);

        coord.Owner.Should().Be("group/subgroup");
        coord.Repo.Should().Be("service");
        coord.Host.Should().Be("gitlab.corp");
        coord.ProjectId.Should().Be(777);
    }

    [Fact(DisplayName = "GitLab: host приводится к нижнему регистру")]
    public void GitLab_host_is_lowercased()
    {
        var coord = new RepoCoordinate(
            GitProviderNames.GitLab, "group", "service", Host: "GitLab.Corp");

        coord.Host.Should().Be("gitlab.corp");
    }

    [Fact(DisplayName = "GitLab: пустой host отклоняется")]
    public void GitLab_requires_host()
    {
        var act = () => new RepoCoordinate(GitProviderNames.GitLab, "group", "service", Host: null);

        act.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "GitLab: host со схемой/путём/пробелом отклоняется")]
    [InlineData("https://gitlab.corp")]
    [InlineData("gitlab.corp/path")]
    [InlineData("git lab.corp")]
    public void GitLab_rejects_malformed_host(string host)
    {
        var act = () => new RepoCoordinate(GitProviderNames.GitLab, "group", "service", Host: host);

        act.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "GitLab: path-traversal и плохие slug-сегменты в owner отклоняются")]
    [InlineData("group/..")]
    [InlineData("group/-bad")]
    [InlineData("group/bad-")]
    [InlineData("group//service")]
    public void GitLab_rejects_bad_owner_segments(string owner)
    {
        var act = () => new RepoCoordinate(GitProviderNames.GitLab, owner, "service", Host: "gitlab.corp");

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "GitLab: неположительный project_id отклоняется")]
    public void GitLab_rejects_non_positive_project_id()
    {
        var act = () => new RepoCoordinate(
            GitProviderNames.GitLab, "group", "service", Host: "gitlab.corp", ProjectId: 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "Неизвестный провайдер отклоняется")]
    public void Unknown_provider_rejected()
    {
        var act = () => new RepoCoordinate("bitbucket", "group", "service", Host: "bb.corp");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
