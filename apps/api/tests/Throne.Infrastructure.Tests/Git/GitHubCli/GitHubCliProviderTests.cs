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

    [Fact(DisplayName = "CloneRepositoryAsync переиспользует существующий клон (no-op при наличии .git)")]
    public async Task Clone_skips_when_target_already_git_repo()
    {
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok(string.Empty));
        var path = Path.Combine(Path.GetTempPath(), $"throne-clone-reuse-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(path, ".git"));
        try
        {
            await _fx.Provider.CloneRepositoryAsync("alice", "throne", path, default);
            _fx.Calls.Should().BeEmpty("папка уже содержит .git — gh repo clone не должен вызываться");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact(DisplayName = "CloneRepositoryAsync удаляет пустой каталог и клонирует")]
    public async Task Clone_removes_empty_target_then_clones()
    {
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok(string.Empty));
        var path = Path.Combine(Path.GetTempPath(), $"throne-clone-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        try
        {
            await _fx.Provider.CloneRepositoryAsync("alice", "throne", path, default);
            _fx.Calls.Single().Arguments.Should()
                .ContainInOrder("repo", "clone", "alice/throne", path);
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    [Fact(DisplayName = "CloneRepositoryAsync на непустой не-git папке кидает GitProviderException")]
    public async Task Clone_throws_when_target_is_dirty_non_git_directory()
    {
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok(string.Empty));
        var path = Path.Combine(Path.GetTempPath(), $"throne-clone-dirty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        await File.WriteAllTextAsync(Path.Combine(path, "stray.txt"), "x");
        try
        {
            var act = async () => await _fx.Provider.CloneRepositoryAsync("alice", "throne", path, default);
            await act.Should().ThrowAsync<Throne.Application.Git.GitProviderException>()
                .Where(ex => ex.Message.Contains("is not a git clone", StringComparison.Ordinal));
            _fx.Calls.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact(DisplayName = "SyncRepositoryAsync вызывает gh repo sync в workspace_path")]
    public async Task Sync_invokes_repo_sync_in_workspace()
    {
        _fx.OnRun(_ => GitHubCliProviderFixture.Ok(string.Empty));

        await _fx.Provider.SyncRepositoryAsync("/tmp/ws", default);

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
