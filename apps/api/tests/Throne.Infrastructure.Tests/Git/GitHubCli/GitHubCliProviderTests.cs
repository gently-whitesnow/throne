using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Throne.Application.Git;
using Throne.Application.Ports;

namespace Throne.Infrastructure.Tests.Git.GitHubCli;

/// <summary>
/// Happy-path coverage of <see cref="Throne.Infrastructure.Git.GitHubCli.GitHubCliProvider"/>
/// — verifies command-line shape and successful parsing per method. Error
/// mapping branches live in <see cref="GitHubCliProviderErrorsTests"/> so this
/// class stays inside the maintainability budget.
/// </summary>
public class GitHubCliProviderTests
{
    private const string MineJson = """
        [{"name":"throne","owner":{"login":"alice"},"defaultBranchRef":{"name":"main"}}]
        """;

    private const string InvolvedJson = """
        [{"name":"shared","owner":{"login":"orgA"},"default_branch":"main","private":true}]
        """;

    private static readonly string[] CloneArgs = ["repo", "clone", "alice/throne", "/tmp/x"];
    private static readonly string[] SyncArgs = ["repo", "sync"];
    private static readonly string[] AuthArgs = ["api", "user", "-i"];

    private readonly GitHubCliProviderFixture _fx = new();

    [Fact(DisplayName = "ListUserRepositoriesAsync вызывает gh repo list --json и парсит вывод")]
    public async Task List_user_repos_runs_repo_list_with_json_flags()
    {
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok(MineJson));

        var repos = await _fx.Provider.ListUserRepositoriesAsync(limit: 10, ct: default);

        repos.Should().ContainSingle().Which.FullName.Should().Be("alice/throne");
        var call = _fx.Calls.Single();
        call.FileName.Should().Be("gh");
        call.Arguments.Should().ContainInOrder("repo", "list", "--limit", "10", "--json");
        call.Arguments.Should().Contain("name,owner,defaultBranchRef,description,isPrivate,url,nameWithOwner");
    }

    [Fact(DisplayName = "SearchRepositoriesAsync(Involved) сливает mine + involved и уникализирует")]
    public async Task Search_involved_merges_and_dedupes()
    {
        const string mineWithSharedJson = """
            [{"name":"throne","owner":{"login":"alice"},"defaultBranchRef":{"name":"main"}},
             {"name":"shared","owner":{"login":"orgA"},"defaultBranchRef":{"name":"main"}}]
            """;

        _fx.OnRun(req =>
            GitHubCliProviderFixture.Ok(GitHubCliProviderFixture.IsApiCall(req) ? InvolvedJson : mineWithSharedJson));

        var repos = await _fx.Provider.SearchRepositoriesAsync(
            RepositorySearchScope.Involved, query: null, limit: 100, ct: default);

        repos.Should().HaveCount(2);
        repos.Select(r => r.FullName).Should().BeEquivalentTo("alice/throne", "orgA/shared");

        var apiCall = _fx.Calls.Single(GitHubCliProviderFixture.IsApiCall);
        apiCall.Arguments.Should().Contain("api");
        apiCall.Arguments.Should().Contain(a => a.Contains("/user/repos", StringComparison.Ordinal));
        apiCall.Arguments.Should().Contain("--paginate");
    }

    [Fact(DisplayName = "SearchRepositoriesAsync с query применяет substring-фильтр")]
    public async Task Search_applies_client_side_query_filter()
    {
        const string mineJson = """
            [{"name":"throne","owner":{"login":"alice"},"defaultBranchRef":{"name":"main"}},
             {"name":"misc","owner":{"login":"alice"},"defaultBranchRef":{"name":"main"}}]
            """;

        _fx.OnRun(_ => GitHubCliProviderFixture.Ok(mineJson));

        var repos = await _fx.Provider.SearchRepositoriesAsync(
            RepositorySearchScope.Mine, query: "thRone", limit: 10, ct: default);

        repos.Should().ContainSingle().Which.Repo.Should().Be("throne");
    }

    [Fact(DisplayName = "CloneRepositoryAsync вызывает gh repo clone owner/repo target")]
    public async Task Clone_invokes_repo_clone()
    {
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok(string.Empty));

        await _fx.Provider.CloneRepositoryAsync("alice", "throne", "/tmp/x", default);

        _fx.Calls.Single().Arguments.Should().BeEquivalentTo(CloneArgs);
    }

    [Fact(DisplayName = "FetchRepositoryAsync вызывает gh repo sync в workspace_path")]
    public async Task Fetch_invokes_repo_sync_in_workspace()
    {
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok(string.Empty));

        await _fx.Provider.FetchRepositoryAsync("/tmp/ws", default);

        var call = _fx.Calls.Single();
        call.Arguments.Should().BeEquivalentTo(SyncArgs);
        call.WorkingDirectory.Should().Be("/tmp/ws");
    }

    [Fact(DisplayName = "GetAuthStatusAsync парсит ok-ответ gh api user -i")]
    public async Task Auth_status_parses_ok_response()
    {
        const string raw = "HTTP/2.0 200 OK\r\nX-OAuth-Scopes: repo\r\n\r\n{\"login\":\"alice\"}";
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok(raw));

        var status = await _fx.Provider.GetAuthStatusAsync(default);

        status.IsAuthenticated.Should().BeTrue();
        status.Account.Should().Be("alice");
        _fx.Calls.Single().Arguments.Should().BeEquivalentTo(AuthArgs);
    }

    [Fact(DisplayName = "GetAuthStatusAsync глотает CliFailure и отдаёт not-authenticated")]
    public async Task Auth_status_swallows_cli_missing()
    {
        _fx.Launcher.RunAsync(Arg.Any<ProcessRunRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new System.ComponentModel.Win32Exception("ENOENT"));

        var status = await _fx.Provider.GetAuthStatusAsync(default);

        status.IsAuthenticated.Should().BeFalse();
        status.Detail.Should().Contain("gh CLI executable");
    }
}
