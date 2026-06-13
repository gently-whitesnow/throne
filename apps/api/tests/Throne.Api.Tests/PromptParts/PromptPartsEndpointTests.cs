using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Throne.Api.Tests.Infrastructure;

namespace Throne.Api.Tests.PromptParts;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class PromptPartsEndpointTests(MongoFixture mongo) : IAsyncLifetime
{
    private static readonly Uri PromptPartsUri = new("/api/v1/prompt-parts", UriKind.Relative);

    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
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
                    ["Mongo:ConnectionString"] = mongo.ConnectionString,
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

    [Fact(DisplayName = "POST /prompt-parts создаёт часть, GET /prompt-parts её возвращает")]
    public async Task Create_then_list_roundtrip()
    {
        var created = await CreatePartAsync("architecture", "arch rules", PromptPartModeNames.Work, "default_on", 0);
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await created.Content.ReadFromJsonAsync<JsonElement>();
        detail.GetProperty("current_version").GetInt32().Should().Be(1);
        detail.GetProperty("mode_roles").GetArrayLength().Should().Be(1);

        // The list returns all scopes (system + user, ADR-0036). The created user part must be present.
        var list = await _client.GetFromJsonAsync<JsonElement>(PromptPartsUri);
        var items = list.EnumerateArray().ToList();
        items.Should().Contain(p =>
            p.GetProperty("scope").GetString() == "user"
            && p.GetProperty("key").GetString() == "architecture"
            && p.GetProperty("text_short").GetString() == "arch rules");
        items.Should().Contain(p => p.GetProperty("scope").GetString() == "system",
            "the list groups system parts alongside user parts");
    }

    [Fact(DisplayName = "POST /prompt-parts с дублирующим key отдаёт 409")]
    public async Task Duplicate_key_returns_409()
    {
        (await CreatePartAsync("dup", "a", PromptPartModeNames.Work, "default_on", 0)).EnsureSuccessStatusCode();
        var second = await CreatePartAsync("dup", "b", PromptPartModeNames.Work, "default_off", 1);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "PUT /prompt-parts/{id}/roles заменяет роли; replace-text бампит версию")]
    public async Task Set_roles_and_replace_text()
    {
        var created = await CreatePartAsync("postgres", "pg rules", PromptPartModeNames.Work, "default_off", 0);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var roles = await _client.PutAsJsonAsync(
            new Uri($"/api/v1/prompt-parts/{id}/roles", UriKind.Relative),
            new RolesBody { Mode_roles = [new RoleBody { Mode = PromptPartModeNames.Interview, Role = "mandatory", Order = 2 }] });
        roles.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterRoles = await roles.Content.ReadFromJsonAsync<JsonElement>();
        afterRoles.GetProperty("mode_roles")[0].GetProperty("mode").GetString().Should().Be(PromptPartModeNames.Interview);

        var replaced = await _client.PostAsJsonAsync(
            new Uri($"/api/v1/prompt-parts/{id}/replace-text", UriKind.Relative),
            new ReplaceBody { Expected_version = 1, Old_text = "pg", New_text = "PG" });
        replaced.StatusCode.Should().Be(HttpStatusCode.OK);
        (await replaced.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("current_version").GetInt32().Should().Be(2);
    }

    [Fact(DisplayName = "DELETE /prompt-parts/{id} без ролей удаляет часть; GET затем 404")]
    public async Task Delete_part_without_roles_returns_200_then_404()
    {
        var created = await _client.PostAsJsonAsync(PromptPartsUri, new CreateBody { Key = "deletable", Text = "x", Mode_roles = [] });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var deleted = await _client.DeleteAsync(new Uri($"/api/v1/prompt-parts/{id}", UriKind.Relative));
        deleted.StatusCode.Should().Be(HttpStatusCode.OK);
        (await deleted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("prompt_part_id").GetString().Should().Be(id);

        var get = await _client.GetAsync(new Uri($"/api/v1/prompt-parts/{id}", UriKind.Relative));
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "DELETE /prompt-parts/{id} с ролями отдаёт 409; после снятия ролей удаляет")]
    public async Task Delete_part_with_roles_returns_409_then_succeeds_after_detach()
    {
        var created = await CreatePartAsync("with-roles", "x", PromptPartModeNames.Work, "default_on", 0);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var blocked = await _client.DeleteAsync(new Uri($"/api/v1/prompt-parts/{id}", UriKind.Relative));
        blocked.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var detach = await _client.PutAsJsonAsync(
            new Uri($"/api/v1/prompt-parts/{id}/roles", UriKind.Relative),
            new RolesBody { Mode_roles = [] });
        detach.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleted = await _client.DeleteAsync(new Uri($"/api/v1/prompt-parts/{id}", UriKind.Relative));
        deleted.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "DELETE /prompt-parts/{id} для неизвестного id отдаёт 404")]
    public async Task Delete_unknown_part_returns_404()
    {
        var deleted = await _client.DeleteAsync(new Uri("/api/v1/prompt-parts/missing", UriKind.Relative));
        deleted.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST /terminal/preview(work) отдаёт mandatory части и тело интента как user_prompt")]
    public async Task Preview_returns_mandatory_and_intent_body()
    {
        var createIntent = await _client.PostAsJsonAsync(
            new Uri("/api/v1/intents", UriKind.Relative), new IntentBody { Text = "do the thing" });
        createIntent.EnsureSuccessStatusCode();
        var intentId = (await createIntent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var preview = await _client.PostAsJsonAsync(
            new Uri($"/api/v1/intents/{intentId}/terminal/preview", UriKind.Relative),
            new PreviewBody { Mode = "work" });

        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await preview.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("user_prompt").GetString().Should().Be("do the thing");
        body.GetProperty("system_prompt").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("parts").EnumerateArray()
            .Should().Contain(p =>
                p.GetProperty("scope").GetString() == "system"
                && p.GetProperty("key").GetString() == "common"
                && p.GetProperty("role").GetString() == "mandatory");
    }

    [Fact(DisplayName = "POST /terminal/preview для неизвестного интента отдаёт 404")]
    public async Task Preview_unknown_intent_returns_404()
    {
        var preview = await _client.PostAsJsonAsync(
            new Uri("/api/v1/intents/missing/terminal/preview", UriKind.Relative),
            new PreviewBody { Mode = "work" });
        preview.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private Task<HttpResponseMessage> CreatePartAsync(string key, string text, string mode, string role, int order) =>
        _client.PostAsJsonAsync(PromptPartsUri, new CreateBody
        {
            Key = key,
            Text = text,
            Mode_roles = [new RoleBody { Mode = mode, Role = role, Order = order }],
        });

    private static class PromptPartModeNames
    {
        public const string Work = "work";
        public const string Interview = "interview";
    }

    private sealed class CreateBody
    {
        [JsonPropertyName("key")] public string Key { get; init; } = string.Empty;
        [JsonPropertyName("text")] public string Text { get; init; } = string.Empty;
        [JsonPropertyName("mode_roles")] public List<RoleBody> Mode_roles { get; init; } = [];
    }

    private sealed class RolesBody
    {
        [JsonPropertyName("mode_roles")] public List<RoleBody> Mode_roles { get; init; } = [];
    }

    private sealed class RoleBody
    {
        [JsonPropertyName("mode")] public string Mode { get; init; } = string.Empty;
        [JsonPropertyName("role")] public string Role { get; init; } = string.Empty;
        [JsonPropertyName("order")] public int Order { get; init; }
    }

    private sealed class ReplaceBody
    {
        [JsonPropertyName("expected_version")] public int Expected_version { get; init; }
        [JsonPropertyName("old_text")] public string Old_text { get; init; } = string.Empty;
        [JsonPropertyName("new_text")] public string New_text { get; init; } = string.Empty;
    }

    private sealed class IntentBody
    {
        [JsonPropertyName("text")] public string Text { get; init; } = string.Empty;
    }

    private sealed class PreviewBody
    {
        [JsonPropertyName("mode")] public string Mode { get; init; } = string.Empty;
    }
}
