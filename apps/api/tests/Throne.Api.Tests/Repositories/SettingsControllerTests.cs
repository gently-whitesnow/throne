using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using Throne.Api.Tests.Infrastructure;
using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Api.Tests.Repositories;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class SettingsControllerTests(SqliteFixture sqlite) : IAsyncLifetime, IDisposable
{
    private RepositoriesApiFixture _fixture = null!;

    public Task InitializeAsync()
    {
        _fixture = new RepositoriesApiFixture(sqlite, TestGitProvider.Create());
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    void IDisposable.Dispose() { /* IAsyncLifetime.DisposeAsync owns cleanup */ }

    [Fact(DisplayName = "GET /api/v1/settings/git-providers/status возвращает catalog entry github={authenticated, login, scopes}")]
    public async Task GitProviders_returns_github_auth_status()
    {
        var response = await _fixture.Client.GetAsync(new Uri("/api/v1/settings/git-providers/status", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>();
        var github = ProviderStatus(dto, GitProviderNames.GitHub);
        github.GetProperty("authenticated").GetBoolean().Should().BeTrue();
        github.GetProperty("login").GetString().Should().Be("octocat");
        github.GetProperty("scopes").EnumerateArray()
            .Select(e => e.GetString()).Should().Equal("repo", "read:org");
    }

    [Fact(DisplayName = "GET /api/v1/settings/git-providers/status — unauthenticated github → authenticated=false + error")]
    public async Task GitProviders_returns_error_when_unauthenticated()
    {
        _fixture.Provider.GetAuthStatusAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProviderAuthStatus(
                Provider: GitProviderNames.GitHub,
                IsAuthenticated: false,
                Detail: "gh auth login required")));

        var response = await _fixture.Client.GetAsync(new Uri("/api/v1/settings/git-providers/status", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>();
        var github = ProviderStatus(dto, GitProviderNames.GitHub);
        github.GetProperty("authenticated").GetBoolean().Should().BeFalse();
        github.GetProperty("error").GetString().Should().Be("gh auth login required");
    }

    [Fact(DisplayName = "GET /api/v1/settings/workspace возвращает resolved root + calculating на первом запросе")]
    public async Task Workspace_first_call_returns_calculating()
    {
        var response = await _fixture.Client.GetAsync(new Uri("/api/v1/settings/workspace", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>();
        dto.GetProperty("root").GetString().Should().Be(_fixture.WorkspaceRoot);
        dto.GetProperty("status").GetString().Should().Be("calculating");
    }

    private static JsonElement ProviderStatus(JsonElement dto, string provider)
    {
        var entry = dto.GetProperty("providers")
            .EnumerateArray()
            .Single(e => e.GetProperty("provider").GetString() == provider);
        return entry.GetProperty("status");
    }
}
