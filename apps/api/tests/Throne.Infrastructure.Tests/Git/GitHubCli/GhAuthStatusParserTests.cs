using FluentAssertions;
using Throne.Application.Ports;
using Throne.Infrastructure.Git.GitHubCli;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

public class GhAuthStatusParserTests
{
    [Fact(DisplayName = "ParseUserResponse читает login и scopes из gh api user -i")]
    public void Reads_login_and_scopes_on_success()
    {
        const string raw = "HTTP/2.0 200 OK\r\n"
            + "X-OAuth-Scopes: repo, read:org, gist\r\n"
            + "Content-Type: application/json\r\n"
            + "\r\n"
            + "{\"login\":\"alice\",\"id\":1}";
        var result = new ProcessRunResult(ExitCode: 0, StandardOutput: raw, StandardError: string.Empty, Elapsed: TimeSpan.Zero);

        var status = GhAuthStatusParser.ParseUserResponse(result, "github.com");

        status.Provider.Should().Be("github");
        status.IsAuthenticated.Should().BeTrue();
        status.Account.Should().Be("alice");
        status.Host.Should().Be("github.com");
        status.Detail.Should().Be("scopes: repo, read:org, gist");
    }

    [Fact(DisplayName = "ParseUserResponse возвращает Unauthenticated при exit!=0")]
    public void Failed_exit_returns_unauthenticated()
    {
        var result = new ProcessRunResult(ExitCode: 1, StandardOutput: string.Empty, StandardError: "HTTP 401: Bad credentials", Elapsed: TimeSpan.Zero);

        var status = GhAuthStatusParser.ParseUserResponse(result, "github.com");

        status.IsAuthenticated.Should().BeFalse();
        status.Account.Should().BeNull();
        status.Detail.Should().Contain("Bad credentials");
    }

    [Fact(DisplayName = "ParseUserResponse терпит отсутствие login в теле")]
    public void Missing_login_marks_unauthenticated()
    {
        const string raw = "HTTP/2.0 200 OK\r\n\r\n{\"id\":1}";
        var result = new ProcessRunResult(ExitCode: 0, StandardOutput: raw, StandardError: string.Empty, Elapsed: TimeSpan.Zero);

        var status = GhAuthStatusParser.ParseUserResponse(result, "github.com");

        status.IsAuthenticated.Should().BeFalse();
        status.Account.Should().BeNull();
    }

    [Fact(DisplayName = "ParseUserResponse терпит мусорное тело без падения")]
    public void Garbage_body_falls_back_safely()
    {
        const string raw = "HTTP/2.0 200 OK\r\n\r\nnot json at all";
        var result = new ProcessRunResult(ExitCode: 0, StandardOutput: raw, StandardError: string.Empty, Elapsed: TimeSpan.Zero);

        var status = GhAuthStatusParser.ParseUserResponse(result, "github.com");

        status.IsAuthenticated.Should().BeFalse();
    }
}
