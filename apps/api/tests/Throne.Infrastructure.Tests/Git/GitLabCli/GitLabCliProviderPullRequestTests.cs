using FluentAssertions;
using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Tests.Git.GitLabCli;

public class GitLabCliProviderPullRequestTests
{
    private readonly GitLabCliProviderFixture _fx = new();

    [Theory(DisplayName = "GetPullRequestAsync нормализует состояния GitLab MR")]
    [InlineData("opened", PullRequestStateNames.Open)]
    [InlineData("locked", PullRequestStateNames.Open)]
    [InlineData("closed", PullRequestStateNames.Closed)]
    [InlineData("merged", PullRequestStateNames.Merged)]
    public async Task Get_pull_request_normalizes_state(string rawState, string expected)
    {
        var json = $$"""
            {"iid":42,"state":"{{rawState}}","title":"MR title","web_url":"https://gitlab.example.com/g/r/-/merge_requests/42"}
            """;
        _fx.OnRun(_ => GitLabCliProviderFixture.Ok(json));

        var snapshot = await _fx.Provider.GetPullRequestAsync("g", "r", 42, default);

        snapshot.Should().NotBeNull();
        snapshot!.State.Should().Be(expected);
        snapshot.Title.Should().Be("MR title");
        _fx.Calls.Single().Arguments.Should().BeEquivalentTo(
            ["api", "projects/g%2Fr/merge_requests/42"],
            o => o.WithStrictOrdering());
    }

    [Fact(DisplayName = "GetPullRequestAsync возвращает null на 404")]
    public async Task Get_pull_request_returns_null_on_404()
    {
        _fx.OnRun(_ => GitLabCliProviderFixture.Fail(1, "HTTP 404: 404 Project Not Found"));

        var snapshot = await _fx.Provider.GetPullRequestAsync("g", "r", 42, default);

        snapshot.Should().BeNull();
    }

    [Fact(DisplayName = "GetPullRequestAsync мапит сетевой сбой в NetworkError")]
    public async Task Get_pull_request_maps_network_failure()
    {
        _fx.OnRun(_ => GitLabCliProviderFixture.Fail(1, "HTTP 503: Service Unavailable"));

        var act = async () => await _fx.Provider.GetPullRequestAsync("g", "r", 42, default);

        var ex = (await act.Should().ThrowAsync<GitProviderException>()).Which;
        ex.Kind.Should().Be(GitProviderErrorKind.NetworkError);
    }
}
