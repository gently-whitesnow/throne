using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Throne.Api.Tests.Infrastructure;

namespace Throne.Api.Tests.Intents;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class GetIntentContextsEndpointTests(MongoFixture mongo) : IAsyncLifetime
{
    // ACTIVE bucket statuses (non-archive, non-fridge) used by the "untagged" / tag contexts.
    private const string ActiveStatusQuery =
        "status=draft&status=interview&status=ready_for_work&status=work&status=awaiting_operator";

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

    [Fact(DisplayName = "GET /api/v1/intents/contexts отдаёт нули на пустом наборе")]
    public async Task Returns_zeros_when_empty()
    {
        var counts = await GetContextsAsync();

        counts.InboxHelp.Should().Be(0);
        counts.Fridge.Should().Be(0);
        counts.Archive.Should().Be(0);
        counts.Pinned.Should().Be(0);
        counts.Untagged.Should().Be(0);
        counts.ArchiveUntagged.Should().Be(0);
        counts.FridgeUntagged.Should().Be(0);
        counts.Tags.Should().BeEmpty();
        counts.ArchiveTags.Should().BeEmpty();
        counts.FridgeTags.Should().BeEmpty();
    }

    [Fact(DisplayName = "GET /api/v1/intents/contexts считает бакеты и совпадает с LIST по фильтрам контекста")]
    public async Task Counts_match_list_filters()
    {
        var a1 = await CreateAsync("a1", ["alpha"]);
        await CreateAsync("a2", ["alpha", "beta"]);
        await CreateAsync("a3", []);

        var help = await CreateAsync("h1", []);
        await SetStatusAsync(help.Id, "awaiting_operator");
        var fridge = await CreateAsync("f1", []);
        await SetStatusAsync(fridge.Id, "fridge");
        var fridgeTagged = await CreateAsync("f2", ["delta"]);
        await SetStatusAsync(fridgeTagged.Id, "fridge");

        var done = await CreateAsync("d1", ["gamma"]);
        await SetStatusAsync(done.Id, "done");
        var rejected = await CreateAsync("x1", []);
        await SetStatusAsync(rejected.Id, "reject", reason: "rejected");

        var alphaTagId = a1.Tags.Single(t => t.Name == "alpha").Id;
        await PinAsync(a1.Id, alphaTagId);

        var counts = await GetContextsAsync();

        counts.InboxHelp.Should().Be(1);
        counts.Fridge.Should().Be(2); // f1 + f2
        counts.Archive.Should().Be(2);
        counts.Pinned.Should().Be(1);
        counts.Untagged.Should().Be(2); // a3 + h1 (active, no tags)
        counts.ArchiveUntagged.Should().Be(1); // x1
        counts.FridgeUntagged.Should().Be(1); // f1
        counts.Tags.Should().BeEquivalentTo(
            new[] { new TagCountView { Tag = "alpha", Count = 2 }, new TagCountView { Tag = "beta", Count = 1 } },
            o => o.WithStrictOrdering());
        counts.ArchiveTags.Should().BeEquivalentTo(
            new[] { new TagCountView { Tag = "gamma", Count = 1 } });
        counts.FridgeTags.Should().BeEquivalentTo(
            new[] { new TagCountView { Tag = "delta", Count = 1 } });

        // Definition of done: every bucket equals what LIST returns with the matching context filter.
        (await ListCountAsync("status=awaiting_operator")).Should().Be(counts.InboxHelp);
        (await ListCountAsync("status=fridge")).Should().Be(counts.Fridge);
        (await ListCountAsync("status=done&status=reject")).Should().Be(counts.Archive);
        (await ListCountAsync("pinned=true")).Should().Be(counts.Pinned);
        (await ListCountAsync($"{ActiveStatusQuery}&untagged=true")).Should().Be(counts.Untagged);
        (await ListCountAsync($"{ActiveStatusQuery}&tag=alpha")).Should().Be(2);
        (await ListCountAsync($"{ActiveStatusQuery}&tag=beta")).Should().Be(1);
        (await ListCountAsync("status=done&status=reject&untagged=true")).Should().Be(counts.ArchiveUntagged);
        (await ListCountAsync("status=done&status=reject&tag=gamma")).Should().Be(1);
        (await ListCountAsync("status=fridge&untagged=true")).Should().Be(counts.FridgeUntagged);
        (await ListCountAsync("status=fridge&tag=delta")).Should().Be(1);
    }

    private async Task<IntentDetailView> CreateAsync(string text, string[] tagNames)
    {
        var response = await _client.PostAsJsonAsync(
            new Uri("/api/v1/intents", UriKind.Relative),
            new { text, tag_names = tagNames });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IntentDetailView>())!;
    }

    private async Task SetStatusAsync(string id, string status, string? reason = null)
    {
        object body = reason is null ? new { status } : new { status, reason };
        var response = await _client.PostAsJsonAsync(
            new Uri($"/api/v1/intents/{id}/status", UriKind.Relative), body);
        response.EnsureSuccessStatusCode();
    }

    private async Task PinAsync(string id, string contextTagId)
    {
        var response = await _client.PostAsJsonAsync(
            new Uri($"/api/v1/intents/{id}/pin", UriKind.Relative),
            new { context_tag_id = contextTagId });
        response.EnsureSuccessStatusCode();
    }

    private async Task<ContextCountsView> GetContextsAsync()
    {
        var response = await _client.GetAsync(new Uri("/api/v1/intents/contexts", UriKind.Relative));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContextCountsView>())!;
    }

    private async Task<int> ListCountAsync(string query)
    {
        var response = await _client.GetAsync(new Uri($"/api/v1/intents?{query}", UriKind.Relative));
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<IntentListPageView>();
        return page!.Items?.Count ?? 0;
    }

    private sealed class IntentDetailView
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; init; } = string.Empty;

        [JsonPropertyName("tags")]
        public IReadOnlyList<TagRefView> Tags { get; init; } = [];
    }

    private sealed class TagRefView
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;
    }

    private sealed class ContextCountsView
    {
        [JsonPropertyName("inbox_help")]
        public int InboxHelp { get; init; }

        [JsonPropertyName("fridge")]
        public int Fridge { get; init; }

        [JsonPropertyName("archive")]
        public int Archive { get; init; }

        [JsonPropertyName("pinned")]
        public int Pinned { get; init; }

        [JsonPropertyName("untagged")]
        public int Untagged { get; init; }

        [JsonPropertyName("archive_untagged")]
        public int ArchiveUntagged { get; init; }

        [JsonPropertyName("fridge_untagged")]
        public int FridgeUntagged { get; init; }

        [JsonPropertyName("tags")]
        public IReadOnlyList<TagCountView> Tags { get; init; } = [];

        [JsonPropertyName("archive_tags")]
        public IReadOnlyList<TagCountView> ArchiveTags { get; init; } = [];

        [JsonPropertyName("fridge_tags")]
        public IReadOnlyList<TagCountView> FridgeTags { get; init; } = [];
    }

    private sealed class TagCountView
    {
        [JsonPropertyName("tag")]
        public string Tag { get; init; } = string.Empty;

        [JsonPropertyName("count")]
        public int Count { get; init; }
    }

    private sealed class IntentListPageView
    {
        [JsonPropertyName("items")]
        public IReadOnlyList<JsonElement>? Items { get; init; }
    }
}
