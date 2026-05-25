using FluentAssertions;
using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

/// <summary>
/// Coverage for <see cref="Throne.Infrastructure.Git.GitHubCli.GitHubCliProvider.GetPullRequestAsync"/>.
/// Exercises the four ADR-0024 § 4/5 outcomes: 200 → snapshot, 404 → null,
/// rate-limit (<c>X-RateLimit-Remaining: 0</c>) → typed network exception,
/// and the <c>merged: true</c> override on a closed PR.
/// </summary>
public class GitHubCliProviderPullRequestTests
{
    private static readonly string[] PullArgs =
        ["api", "-i", "/repos/alice/throne/pulls/42"];

    private readonly GitHubCliProviderFixture _fx = new();

    [Fact(DisplayName = "GetPullRequest парсит 200 в PullRequestSnapshot")]
    public async Task Get_pull_request_parses_ok_snapshot()
    {
        const string raw = "HTTP/2.0 200 OK\r\nETag: \"abc\"\r\n\r\n"
            + "{\"number\":42,\"state\":\"open\",\"merged\":false,"
            + "\"title\":\"feat: thing\",\"html_url\":\"https://gh/x/y/pull/42\"}";
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok(raw));

        var snapshot = await _fx.Provider.GetPullRequestAsync("alice", "throne", 42, default);

        snapshot.Should().NotBeNull();
        snapshot!.Number.Should().Be(42);
        snapshot.State.Should().Be(PullRequestStateNames.Open);
        snapshot.Title.Should().Be("feat: thing");
        _fx.Calls.Single().Arguments.Should().BeEquivalentTo(PullArgs);
    }

    [Fact(DisplayName = "GetPullRequest мапит closed+merged в state=merged")]
    public async Task Get_pull_request_maps_closed_merged_to_merged()
    {
        const string raw = "HTTP/2.0 200 OK\r\n\r\n"
            + "{\"number\":7,\"state\":\"closed\",\"merged\":true,\"title\":\"chore\"}";
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok(raw));

        var snapshot = await _fx.Provider.GetPullRequestAsync("alice", "throne", 7, default);

        snapshot!.State.Should().Be(PullRequestStateNames.Merged);
    }

    [Fact(DisplayName = "GetPullRequest возвращает null на HTTP 404")]
    public async Task Get_pull_request_returns_null_on_404()
    {
        const string raw = "HTTP/2.0 404 Not Found\r\n\r\n{\"message\":\"Not Found\"}";
        _fx.OnRun(_ => new global::Throne.Application.Ports.ProcessRunResult(
            ExitCode: 1, StandardOutput: raw, StandardError: "HTTP 404: Not Found", Elapsed: TimeSpan.Zero));

        var snapshot = await _fx.Provider.GetPullRequestAsync("alice", "throne", 42, default);

        snapshot.Should().BeNull();
    }

    [Fact(DisplayName = "GetPullRequest на X-RateLimit-Remaining:0 бросает GitProviderException(NetworkError)")]
    public async Task Get_pull_request_throws_on_rate_limit()
    {
        const string raw = "HTTP/2.0 403 Forbidden\r\n"
            + "X-RateLimit-Remaining: 0\r\nX-RateLimit-Reset: 1700000000\r\n\r\n"
            + "{\"message\":\"API rate limit exceeded\"}";
        _fx.OnRun(_ => new global::Throne.Application.Ports.ProcessRunResult(
            ExitCode: 1, StandardOutput: raw, StandardError: "HTTP 403", Elapsed: TimeSpan.Zero));

        var act = async () => await _fx.Provider.GetPullRequestAsync("alice", "throne", 42, default);

        var ex = (await act.Should().ThrowAsync<GitProviderException>()).Which;
        ex.Kind.Should().Be(GitProviderErrorKind.NetworkError);
        ex.Message.Should().Contain("rate limit");
    }
}
