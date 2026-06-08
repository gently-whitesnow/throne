using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using Throne.Api.Tests.Infrastructure;
using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Api.Tests.Repositories;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class GitProvidersControllerTests(MongoFixture mongo) : IAsyncLifetime, IDisposable
{
    private RepositoriesApiFixture _fixture = null!;

    public Task InitializeAsync()
    {
        _fixture = new RepositoriesApiFixture(mongo, TestGitProvider.Create());
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    void IDisposable.Dispose() { /* IAsyncLifetime.DisposeAsync owns cleanup */ }

    [Fact(DisplayName = "GET /api/v1/git-providers/github/repositories/search возвращает результат провайдера")]
    public async Task Search_returns_provider_result()
    {
        var refs = new List<GitRepositoryRef>
        {
            new(GitProviderNames.GitHub, "octo", "hello", "main") { HtmlUrl = "https://github.com/octo/hello" },
            new(GitProviderNames.GitHub, "octo", "world", "trunk"),
        };
        _fixture.Provider.SearchRepositoriesAsync(
                Arg.Any<RepositorySearchScope>(),
                Arg.Any<string?>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GitRepositoryRef>>(refs));

        var response = await _fixture.Client.GetAsync(
            new Uri("/api/v1/git-providers/github/repositories/search?q=hello&scope=involved&limit=50", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        items.Should().NotBeNull().And.HaveCount(2);
        items![0].GetProperty("full_name").GetString().Should().Be("octo/hello");
        items[0].GetProperty("provider").GetString().Should().Be("github");
        await _fixture.Provider.Received(1).SearchRepositoriesAsync(
            RepositorySearchScope.Involved, "hello", 50, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "GET /api/v1/git-providers/gitlab/repositories/search делегирует GitLab provider")]
    public async Task Search_uses_provider_from_route()
    {
        await _fixture.DisposeAsync();
        _fixture = new RepositoriesApiFixture(mongo, TestGitProvider.Create(
            GitProviderNames.GitLab,
            "gitlab.example.com"));
        _fixture.Provider.SearchRepositoriesAsync(
                Arg.Any<RepositorySearchScope>(),
                Arg.Any<string?>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GitRepositoryRef>>(
                new[] { new GitRepositoryRef(GitProviderNames.GitLab, "team/platform", "core", "main") }));

        var response = await _fixture.Client.GetAsync(
            new Uri("/api/v1/git-providers/gitlab/repositories/search?q=core&scope=mine&limit=20", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        items.Should().NotBeNull().And.ContainSingle();
        items![0].GetProperty("provider").GetString().Should().Be("gitlab");
        items[0].GetProperty("full_name").GetString().Should().Be("team/platform/core");
        await _fixture.Provider.Received(1).SearchRepositoriesAsync(
            RepositorySearchScope.Mine, "core", 20, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "GET .../search для неаутентифицированного провайдера всё равно делегирует — 422 рисует провайдер сам, не контроллер")]
    public async Task Search_propagates_provider_call()
    {
        var response = await _fixture.Client.GetAsync(new Uri("/api/v1/git-providers/github/repositories/search", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _fixture.Provider.Received(1).SearchRepositoriesAsync(
            RepositorySearchScope.Mine, null, Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "GET /api/v1/git-providers/github/repositories/my возвращает результат list-mine")]
    public async Task ListMy_returns_provider_result()
    {
        _fixture.Provider.ListUserRepositoriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GitRepositoryRef>>(
                new[] { new GitRepositoryRef(GitProviderNames.GitHub, "octo", "alpha", "main") }));

        var response = await _fixture.Client.GetAsync(new Uri("/api/v1/git-providers/github/repositories/my", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        items!.Should().ContainSingle().Which.GetProperty("repo").GetString().Should().Be("alpha");
    }

    [Fact(DisplayName = "GET .../{owner}/{repo}/branches возвращает список веток и маркирует default")]
    public async Task ListBranches_returns_provider_result()
    {
        _fixture.Provider.ListBranchesAsync(
                "octo", "hello", Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GitBranchRef>>(
                new[]
                {
                    new GitBranchRef("main", true),
                    new GitBranchRef("feature/x", false),
                }));

        var response = await _fixture.Client.GetAsync(new Uri(
            "/api/v1/git-providers/github/repositories/octo/hello/branches?q=feat&limit=10", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        items.Should().NotBeNull().And.HaveCount(2);
        items![0].GetProperty("name").GetString().Should().Be("main");
        items[0].GetProperty("is_default").GetBoolean().Should().BeTrue();
        items[1].GetProperty("is_default").GetBoolean().Should().BeFalse();
        await _fixture.Provider.Received(1).ListBranchesAsync(
            "octo", "hello", "feat", 10, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "GET .../{owner}/{repo}/pulls возвращает список открытых PR")]
    public async Task ListPulls_returns_provider_result()
    {
        _fixture.Provider.ListPullRequestsAsync(
                "octo", "hello", Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GitPullRequestRef>>(
                new[]
                {
                    new GitPullRequestRef(42, "fix: thing", "fix/thing", PullRequestStateNames.Open),
                }));

        var response = await _fixture.Client.GetAsync(new Uri(
            "/api/v1/git-providers/github/repositories/octo/hello/pulls?limit=10", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        items.Should().NotBeNull().And.ContainSingle();
        items![0].GetProperty("number").GetInt32().Should().Be(42);
        items[0].GetProperty("head_ref").GetString().Should().Be("fix/thing");
        items[0].GetProperty("state").GetString().Should().Be("open");
        await _fixture.Provider.Received(1).ListPullRequestsAsync(
            "octo", "hello", null, 10, Arg.Any<CancellationToken>());
    }
}
