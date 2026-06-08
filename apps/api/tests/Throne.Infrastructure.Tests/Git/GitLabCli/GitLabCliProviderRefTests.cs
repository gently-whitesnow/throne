using FluentAssertions;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Tests.Git.GitLabCli;

public class GitLabCliProviderRefTests
{
    private readonly GitLabCliProviderFixture _fx = new();

    [Fact(DisplayName = "ListBranchesAsync парсит branches и default-флаг")]
    public async Task List_branches_parses_default_flag()
    {
        const string json = """
            [{"name":"main","default":true},{"name":"feature/gitlab","default":false}]
            """;
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok(json));

        var branches = await _fx.Provider.ListBranchesAsync(
            "group/sub", "throne", query: "feature", limit: 10, ct: default);

        branches.Should().ContainSingle().Which.Name.Should().Be("feature/gitlab");
        var call = _fx.Calls.Single();
        call.Arguments.Should().BeEquivalentTo(
            ["api", "projects/group%2Fsub%2Fthrone/repository/branches?per_page=10"],
            o => o.WithStrictOrdering());
        GitLabCliProviderFixture.HasGitLabHost(call).Should().BeTrue();
    }

    [Fact(DisplayName = "ListPullRequestsAsync парсит MR и нормализует locked → open")]
    public async Task List_pull_requests_parses_open_merge_requests()
    {
        const string json = """
            [{"iid":7,"title":"GitLab read flow","source_branch":"feat/gitlab","state":"locked"}]
            """;
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok(json));

        var prs = await _fx.Provider.ListPullRequestsAsync(
            "group/sub", "throne", query: "7", limit: 5, ct: default);

        var pr = prs.Should().ContainSingle().Subject;
        pr.Number.Should().Be(7);
        pr.State.Should().Be(PullRequestStateNames.Open);
        var call = _fx.Calls.Single();
        call.Arguments.Should().BeEquivalentTo(
            ["api", "projects/group%2Fsub%2Fthrone/merge_requests?state=opened&per_page=5"],
            o => o.WithStrictOrdering());
        GitLabCliProviderFixture.HasGitLabHost(call).Should().BeTrue();
    }
}
