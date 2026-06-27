using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Tests.Infrastructure;
using Throne.Application.TaskTrackers;

namespace Throne.Api.Tests.TaskTrackers;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class TaskTrackerCatalogEndpointTests(SqliteFixture sqlite) : IAsyncLifetime
{
    private sealed record FakeProvider(string TrackerKey, string DisplayName) : ITaskTrackerProvider;

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private SqliteTestDatabase _database = null!;

    public Task InitializeAsync()
    {
        _database = sqlite.CreateDatabase();
        _factory = SqliteTestHost.Create(_database).WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<ITaskTrackerProvider>(new FakeProvider("kaiten", "Kaiten"));
                services.AddSingleton<ITaskTrackerProvider>(new FakeProvider("jira", "Jira"));
            }));
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact(DisplayName = "GET /task-trackers: каждый зарегистрированный провайдер ровно один раз, в порядке регистрации")]
    public async Task Lists_each_registered_provider_once()
    {
        var response = await _client.GetAsync(new Uri("/api/v1/task-trackers", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        var trackers = doc.RootElement.GetProperty("providers")
            .EnumerateArray()
            .Select(p => p.GetProperty("tracker").GetString())
            .ToArray();

        trackers.Should().Equal("kaiten", "jira");
    }

    [Fact(DisplayName = "GET /task-trackers/{known}: отдаёт строковый ключ и display_name")]
    public async Task Resolves_known_provider()
    {
        var response = await _client.GetAsync(new Uri("/api/v1/task-trackers/kaiten", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        root.GetProperty("tracker").GetString().Should().Be("kaiten");
        root.GetProperty("tracker").ValueKind.Should().Be(JsonValueKind.String);
        root.GetProperty("display_name").GetString().Should().Be("Kaiten");
    }

    [Fact(DisplayName = "GET /task-trackers/{unknown}: 422 с problem-кодом provider_unsupported")]
    public async Task Unknown_tracker_returns_422()
    {
        var response = await _client.GetAsync(new Uri("/api/v1/task-trackers/trello", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("code").GetString().Should().Be("task_tracker.provider_unsupported");
    }
}
