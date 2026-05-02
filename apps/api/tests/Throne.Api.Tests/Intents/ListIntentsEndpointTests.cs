using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.TextVersions;

namespace Throne.Api.Tests.Intents;

public sealed class ListIntentsEndpointTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly MongoDbContainer _mongo = new MongoDbBuilder().WithReplicaSet().Build();
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _mongo.StartAsync();

        var raw = _mongo.GetConnectionString();
        var separator = raw.Contains('?') ? '&' : '?';
        var connectionString = $"{raw}{separator}directConnection=true";
        var dbName = $"throne_api_{Guid.NewGuid():N}";

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
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _mongo.DisposeAsync();
    }

    [Fact(DisplayName = "GET /api/v1/intents возвращает пустой массив когда intents нет")]
    public async Task Returns_empty_when_no_intents()
    {
        var response = await _client.GetAsync(new Uri("/api/v1/intents", UriKind.Relative));

        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<IntentListItemView>>();
        items.Should().NotBeNull().And.BeEmpty();
    }

    [Fact(DisplayName = "GET /api/v1/intents возвращает intents с text_short, обрезанным до 140 символов")]
    public async Task Returns_intents_with_text_short_truncated()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IIntentRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var shortIntent = Intent.Create(IntentId.New(), "short text", ["a"], Now);
            var shortVersion = TextVersion.CreateSnapshot(
                Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, shortIntent.Id.Value,
                shortIntent.Text, Now, TextVersionAuthor.Agent);

            var longText = new string('x', 200);
            var longIntent = Intent.Create(IntentId.New(), longText, ["b", "c"], Now);
            var longVersion = TextVersion.CreateSnapshot(
                Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, longIntent.Id.Value,
                longText, Now, TextVersionAuthor.Agent);

            await uow.ExecuteAsync(
                ct => repo.CreateAsync(shortIntent, shortVersion, InitialStatusChange(shortIntent), ct),
                CancellationToken.None);
            await uow.ExecuteAsync(
                ct => repo.CreateAsync(longIntent, longVersion, InitialStatusChange(longIntent), ct),
                CancellationToken.None);
        }

        var response = await _client.GetAsync(new Uri("/api/v1/intents", UriKind.Relative));
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadFromJsonAsync<List<IntentListItemView>>();
        raw.Should().NotBeNull().And.HaveCount(2);
        var items = raw!;

        var shortItem = items.Single(i => i.Tags.Contains("a"));
        shortItem.TextShort.Should().Be("short text");
        shortItem.Status.Should().Be("draft");
        shortItem.CurrentVersion.Should().Be(1);

        var longItem = items.Single(i => i.Tags.Contains("b"));
        longItem.TextShort.Should().HaveLength(140).And.Be(new string('x', 140));
    }

    private static IntentStatusChange InitialStatusChange(Intent intent) =>
        IntentStatusChange.Create(
            Guid.NewGuid().ToString("N"),
            intent.Id,
            intent.CurrentVersion,
            intent.Status,
            intent.Status,
            "test:create",
            Now,
            IntentTrainingAuthor.Agent);

    private sealed class IntentListItemView
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("current_version")]
        public int CurrentVersion { get; init; }

        [JsonPropertyName("status")]
        public string Status { get; init; } = string.Empty;

        [JsonPropertyName("tags")]
        public IReadOnlyList<string> Tags { get; init; } = [];

        [JsonPropertyName("text_short")]
        public string TextShort { get; init; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; init; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset UpdatedAt { get; init; }
    }
}
