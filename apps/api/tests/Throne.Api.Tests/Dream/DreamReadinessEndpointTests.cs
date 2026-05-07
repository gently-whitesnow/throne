using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Throne.Api.Tests.Infrastructure;

namespace Throne.Api.Tests.Dream;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class DreamReadinessEndpointTests(MongoFixture mongo) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        var connectionString = mongo.ConnectionString;
        var dbName = $"throne_dream_{Guid.NewGuid():N}";

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseDefaultServiceProvider(o =>
            {
                o.ValidateScopes = false;
                o.ValidateOnBuild = false;
            });
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Mongo:ConnectionString"] = connectionString,
                    ["Mongo:Database"] = dbName,
                });
            });
        });

        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact(DisplayName = "GET /api/v1/dream-runs/readiness без evidence → status=empty, available_tokens=0")]
    public async Task Readiness_returns_empty_on_fresh_db()
    {
        var response = await _client.GetAsync(new Uri("/api/v1/dream-runs/readiness", UriKind.Relative));
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ReadinessView>();
        payload.Should().NotBeNull();
        payload!.Status.Should().Be("empty");
        payload.AvailableTokens.Should().Be(0);
        payload.LockedTokens.Should().Be(0);
        payload.IntentCount.Should().Be(0);
        payload.PendingProposalsCount.Should().Be(0);
        payload.PendingRunsCount.Should().Be(0);
        payload.SuggestedAction.Should().Contain("Wait");
    }

    [Fact(DisplayName = "GET /api/v1/dream-runs/pending/count возвращает 0 для пустой БД")]
    public async Task PendingCount_returns_zero()
    {
        var response = await _client.GetAsync(new Uri("/api/v1/dream-runs/pending/count", UriKind.Relative));
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PendingCountView>();
        payload.Should().NotBeNull();
        payload!.PendingProposalsCount.Should().Be(0);
    }

    [Fact(DisplayName = "GET /api/v1/dream-runs/{id} → 404 для несуществующего id")]
    public async Task Get_run_returns_404_when_missing()
    {
        var response = await _client.GetAsync(new Uri("/api/v1/dream-runs/missing", UriKind.Relative));
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    private sealed class ReadinessView
    {
        [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
        [JsonPropertyName("available_tokens")] public int AvailableTokens { get; init; }
        [JsonPropertyName("locked_tokens")] public int LockedTokens { get; init; }
        [JsonPropertyName("intent_count")] public int IntentCount { get; init; }
        [JsonPropertyName("pending_proposals_count")] public int PendingProposalsCount { get; init; }
        [JsonPropertyName("pending_runs_count")] public int PendingRunsCount { get; init; }
        [JsonPropertyName("suggested_action")] public string SuggestedAction { get; init; } = string.Empty;
    }

    private sealed class PendingCountView
    {
        [JsonPropertyName("pending_proposals_count")] public int PendingProposalsCount { get; init; }
    }
}
