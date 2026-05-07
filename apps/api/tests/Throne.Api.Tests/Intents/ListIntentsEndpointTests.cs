using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Throne.Api.Tests.Infrastructure;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.TextVersions;

namespace Throne.Api.Tests.Intents;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class ListIntentsEndpointTests(MongoFixture mongo) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        var connectionString = mongo.ConnectionString;
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
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
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

            var shortIntent = Intent.Create(IntentId.New(), "local-dev", "short text", [Throne.Domain.Tags.TagId.New()], Now);
            var shortVersion = TextVersion.CreateSnapshot(
                Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, shortIntent.Id.Value,
                shortIntent.Text, Now, TextVersionAuthor.Agent);

            var longText = new string('x', 200);
            var longIntent = Intent.Create(IntentId.New(), "local-dev", longText, [Throne.Domain.Tags.TagId.New(), Throne.Domain.Tags.TagId.New()], Now);
            var longVersion = TextVersion.CreateSnapshot(
                Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, longIntent.Id.Value,
                longText, Now, TextVersionAuthor.Agent);

            await uow.ExecuteAsync(
                ct => repo.CreateAsync(shortIntent, shortVersion, InitialStatusChange(shortIntent), Array.Empty<Throne.Domain.Tags.Tag>(), ct),
                CancellationToken.None);
            await uow.ExecuteAsync(
                ct => repo.CreateAsync(longIntent, longVersion, InitialStatusChange(longIntent), Array.Empty<Throne.Domain.Tags.Tag>(), ct),
                CancellationToken.None);
        }

        var response = await _client.GetAsync(new Uri("/api/v1/intents", UriKind.Relative));
        response.EnsureSuccessStatusCode();

        var raw = await response.Content.ReadFromJsonAsync<List<IntentListItemView>>();
        raw.Should().NotBeNull().And.HaveCount(2);
        var items = raw!;

        var shortItem = items.Single(i => i.TextShort == "short text");
        shortItem.Status.Should().Be("draft");
        shortItem.CurrentVersion.Should().Be(1);

        var longItem = items.Single(i => i.TextShort.StartsWith('x'));
        longItem.TextShort.Should().HaveLength(140).And.Be(new string('x', 140));
    }

    [Fact(DisplayName = "GET /api/v1/intents?status=... возвращает только intents в указанных статусах")]
    public async Task Returns_intents_filtered_by_status()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IIntentRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await SeedAsync(repo, uow, "draft-text");
            var work = await SeedAsync(repo, uow, "work-text");
            var done = await SeedAsync(repo, uow, "done-text");
            var rejected = await SeedAsync(repo, uow, "reject-text");

            await uow.ExecuteAsync(
                ct => repo.SetStatusAsync(work.Id, "work", null, IntentTrainingAuthor.User, "test", Now, ct),
                CancellationToken.None);
            await uow.ExecuteAsync(
                ct => repo.SetStatusAsync(done.Id, "done", null, IntentTrainingAuthor.User, "test", Now, ct),
                CancellationToken.None);
            await uow.ExecuteAsync(
                ct => repo.SetStatusAsync(rejected.Id, "reject", "rejected", IntentTrainingAuthor.User, "test", Now, ct),
                CancellationToken.None);
        }

        var response = await _client.GetAsync(new Uri("/api/v1/intents?status=done&status=reject", UriKind.Relative));
        response.EnsureSuccessStatusCode();

        var items = await response.Content.ReadFromJsonAsync<List<IntentListItemView>>();
        items.Should().NotBeNull();
        items!.Select(i => i.Status).Should().BeEquivalentTo(["done", "reject"]);
    }

    private static async Task<Intent> SeedAsync(IIntentRepository repo, IUnitOfWork uow, string text)
    {
        var intent = Intent.Create(IntentId.New(), "local-dev", text, [Throne.Domain.Tags.TagId.New()], Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, intent.Id.Value,
            intent.Text, Now, TextVersionAuthor.Agent);
        await uow.ExecuteAsync(
            ct => repo.CreateAsync(intent, version, InitialStatusChange(intent), Array.Empty<Throne.Domain.Tags.Tag>(), ct),
            CancellationToken.None);
        return intent;
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
        public IReadOnlyList<TagRefView> Tags { get; init; } = [];

        [JsonPropertyName("text_short")]
        public string TextShort { get; init; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; init; }

        [JsonPropertyName("updated_at")]
        public DateTimeOffset UpdatedAt { get; init; }
    }

    private sealed class TagRefView
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;
    }
}
