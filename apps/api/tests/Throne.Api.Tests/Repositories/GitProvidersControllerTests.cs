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
}
