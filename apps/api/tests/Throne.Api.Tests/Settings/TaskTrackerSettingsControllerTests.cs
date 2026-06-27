using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Throne.Api.Tests.Infrastructure;
using Throne.Api.Tests.Repositories;

namespace Throne.Api.Tests.Settings;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class TaskTrackerSettingsControllerTests(SqliteFixture sqlite) : IAsyncLifetime, IDisposable
{
    private RepositoriesApiFixture _fixture = null!;

    public Task InitializeAsync()
    {
        _fixture = new RepositoriesApiFixture(sqlite, TestGitProvider.Create());
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    void IDisposable.Dispose() { /* IAsyncLifetime.DisposeAsync owns cleanup */ }

    [Fact(DisplayName = "GET /settings/task-trackers lists the kaiten provider as not_configured")]
    public async Task Lists_registered_provider_not_configured()
    {
        var response = await _fixture.Client.GetAsync(
            new Uri("/api/v1/settings/task-trackers", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>();
        var kaiten = dto.GetProperty("connections").EnumerateArray()
            .Single(c => c.GetProperty("tracker").GetString() == "kaiten");
        kaiten.GetProperty("display_name").GetString().Should().Be("Kaiten");
        kaiten.GetProperty("state").GetString().Should().Be("not_configured");
    }

    [Fact(DisplayName = "GET boards without a connection → 409 connection_missing")]
    public async Task Boards_without_connection_conflict()
    {
        var response = await _fixture.Client.GetAsync(
            new Uri("/api/v1/settings/task-trackers/kaiten/boards", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("task_tracker.connection_missing");
    }

    [Fact(DisplayName = "PUT connection for an unknown tracker → 422 provider_unsupported")]
    public async Task Unknown_tracker_unprocessable()
    {
        var response = await _fixture.Client.PutAsJsonAsync(
            new Uri("/api/v1/settings/task-trackers/jira/connection", UriKind.Relative),
            new { base_url = "https://example.atlassian.net", token = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("task_tracker.provider_unsupported");
    }
}
