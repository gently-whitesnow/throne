using FluentAssertions;
using Throne.Application.Git;

namespace Throne.Infrastructure.Tests.Git.GitLabCli;

public class GitLabCliProviderRepositoryTests
{
    private const string OwnedProjectsJson = """
        [{"id":123,"path_with_namespace":"group/sub/throne","default_branch":"main",
          "description":"Cockpit","visibility":"private","web_url":"https://gitlab.example.com/group/sub/throne"}]
        """;

    private readonly GitLabCliProviderFixture _fx = new();

    [Fact(DisplayName = "ListUserRepositoriesAsync вызывает glab api projects owned и парсит GitLab-поля")]
    public async Task List_user_repositories_uses_owned_projects_api()
    {
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok(OwnedProjectsJson));

        var repos = await _fx.Provider.ListUserRepositoriesAsync(limit: 10, ct: default);

        var repo = repos.Should().ContainSingle().Subject;
        repo.Provider.Should().Be("gitlab");
        repo.Host.Should().Be(GitLabCliProviderFixture.Host);
        repo.ProjectId.Should().Be(123);
        repo.FullName.Should().Be("group/sub/throne");
        repo.Private.Should().BeTrue();
        var call = _fx.Calls.Single();
        call.Arguments.Should().BeEquivalentTo(["api", "projects?owned=true&per_page=10"], o => o.WithStrictOrdering());
        GitLabCliProviderFixture.HasGitLabHost(call).Should().BeTrue();
    }

    [Fact(DisplayName = "SearchRepositoriesAsync(Involved) сливает owned + membership и фильтрует query")]
    public async Task Search_involved_merges_membership_projects()
    {
        const string membershipJson = """
            [{"id":456,"path_with_namespace":"org/shared","default_branch":"trunk",
              "visibility":"internal","web_url":"https://gitlab.example.com/org/shared"}]
            """;
        _fx.OnRun(req => GitLabCliProviderFixture.Ok(
            req.Arguments[1].Contains("membership=true", StringComparison.Ordinal) ? membershipJson : OwnedProjectsJson));

        var repos = await _fx.Provider.SearchRepositoriesAsync(
            RepositorySearchScope.Involved, query: "share", limit: 20, ct: default);

        repos.Should().ContainSingle().Which.FullName.Should().Be("org/shared");
        _fx.Calls.Select(c => c.Arguments[1]).Should()
            .Contain("projects?owned=true&per_page=20&search=share&search_namespaces=true");
        _fx.Calls.Select(c => c.Arguments[1]).Should()
            .Contain("projects?membership=true&per_page=20&search=share&search_namespaces=true");
        _fx.Calls.Should().OnlyContain(c => GitLabCliProviderFixture.HasGitLabHost(c));
    }

    [Fact(DisplayName = "SearchRepositoriesAsync прокидывает query в GitLab search, а не фильтрует первую страницу")]
    public async Task Search_forwards_query_to_gitlab_api()
    {
        const string nexusJson = """
            [{"id":2026,"path_with_namespace":"trucks/nexus/nexus","default_branch":"master",
              "visibility":"internal","web_url":"https://gitlab.example.com/trucks/nexus/nexus"}]
            """;
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok(nexusJson));

        await _fx.Provider.SearchRepositoriesAsync(
            RepositorySearchScope.Mine, query: "trucks/nexus", limit: 30, ct: default);

        _fx.Calls.Single().Arguments[1].Should()
            .Be("projects?owned=true&per_page=30&search=trucks%2Fnexus&search_namespaces=true");
    }

    [Fact(DisplayName = "SearchRepositoriesAsync без query не добавляет search-параметр")]
    public async Task Search_without_query_omits_search_param()
    {
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok(OwnedProjectsJson));

        await _fx.Provider.SearchRepositoriesAsync(
            RepositorySearchScope.Mine, query: null, limit: 30, ct: default);

        _fx.Calls.Single().Arguments[1].Should().Be("projects?owned=true&per_page=30");
    }

    [Fact(DisplayName = "CloneRepositoryAsync вызывает glab repo clone с partial clone и GITLAB_HOST")]
    public async Task Clone_invokes_glab_repo_clone()
    {
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok(string.Empty));

        await _fx.Provider.CloneRepositoryAsync("group/sub", "throne", "/tmp/throne", default);

        var call = _fx.Calls.Single();
        call.FileName.Should().Be("glab");
        call.Arguments.Should().BeEquivalentTo(
            ["repo", "clone", "group/sub/throne", "/tmp/throne", "--", "--filter=blob:none"],
            o => o.WithStrictOrdering());
        GitLabCliProviderFixture.HasGitLabHost(call).Should().BeTrue();
    }

    [Fact(DisplayName = "SyncRepositoryAsync делает git fetch в workspace с GITLAB_HOST")]
    public async Task Sync_invokes_git_fetch()
    {
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok(string.Empty));

        await _fx.Provider.SyncRepositoryAsync("/tmp/ws", default);

        var call = _fx.Calls.Single();
        call.FileName.Should().Be("git");
        call.WorkingDirectory.Should().Be("/tmp/ws");
        call.Arguments.Should().BeEquivalentTo(["fetch", "--all", "--prune"], o => o.WithStrictOrdering());
        GitLabCliProviderFixture.HasGitLabHost(call).Should().BeTrue();
    }
}
