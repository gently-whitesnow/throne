using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Throne.Api.Tests.Infrastructure;

namespace Throne.Api.Tests.Repositories;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class PullRequestArtifactsControllerTests(MongoFixture mongo) : IAsyncLifetime, IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 6, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] FirstSourceRefs = ["sha:abc"];
    private static readonly string[] SecondSourceRefs = ["sha:def"];
    private static readonly string[] AffectedEndpoints = ["PUT /api/v1/x"];
    private static readonly string[] AffectedTables = ["orders"];
    private static readonly string[] ModuleNodes = ["core", "leaf"];

    private RepositoriesApiFixture _fixture = null!;

    public Task InitializeAsync()
    {
        _fixture = new RepositoriesApiFixture(mongo, TestGitProvider.Create());
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    void IDisposable.Dispose() { /* IAsyncLifetime.DisposeAsync owns cleanup */ }

    [Fact(DisplayName = "PUT .../artifacts/{type} дважды перезаписывает latest, GET/LIST отдают актуальное")]
    public async Task Put_is_latest_wins()
    {
        var intent = await RepositoriesApiSeed.IntentAsync(_fixture, Now);
        var binding = await RepositoriesApiSeed.ReadyBindingAsync(_fixture, intent.Id, 42, Now);
        var url = Artifact(binding.Id.Value, "static_analysis");

        var first = await _fixture.Client.PutAsJsonAsync(url, new
        {
            render = "markdown",
            content = "# v1",
            summary = "Static analysis",
            source = "static",
            source_refs = FirstSourceRefs,
            produced_at = Now,
        });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await _fixture.Client.PutAsJsonAsync(url, new
        {
            render = "markdown",
            content = "# v2",
            summary = "Static analysis updated",
            source = "agent",
            source_refs = SecondSourceRefs,
            produced_at = Now.AddMinutes(1),
        });
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await second.Content.ReadFromJsonAsync<JsonElement>();
        dto.GetProperty("pull_request_number").GetInt32().Should().Be(42);
        dto.GetProperty("content").GetString().Should().Be("# v2");
        dto.GetProperty("produced_at").GetDateTimeOffset().Should().Be(Now.AddMinutes(1));

        var found = await _fixture.Client.GetAsync(url);
        found.StatusCode.Should().Be(HttpStatusCode.OK);
        (await found.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("content").GetString().Should().Be("# v2");

        var list = await _fixture.Client.GetAsync(Artifacts(binding.Id.Value));
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await list.Content.ReadFromJsonAsync<List<JsonElement>>();
        items.Should().ContainSingle();
        items![0].GetProperty("type").GetString().Should().Be("static_analysis");
    }

    [Fact(DisplayName = "PUT review_recommendation сохраняет типизированный content и head_sha, GET отдаёт объект")]
    public async Task Put_round_trips_typed_review_recommendation()
    {
        var intent = await RepositoriesApiSeed.IntentAsync(_fixture, Now);
        var binding = await RepositoriesApiSeed.ReadyBindingAsync(_fixture, intent.Id, 42, Now);
        var url = Artifact(binding.Id.Value, "review_recommendation");

        var put = await _fixture.Client.PutAsJsonAsync(url, new
        {
            render = "markdown",
            content = "## Review recommendation\nStart with the core.",
            summary = "Read core first",
            source = "agent",
            source_refs = FirstSourceRefs,
            head_sha = "abc1234",
            review_recommendation = new
            {
                file_order = new[]
                {
                    new { path = "src/Core.cs", reason = "entry point", risk = "high" },
                    new { path = "src/Leaf.cs", reason = "trivial", risk = "low" },
                },
                affected_endpoints = AffectedEndpoints,
                affected_db_tables = AffectedTables,
                module_graph = new
                {
                    nodes = ModuleNodes,
                    edges = new[] { new { from = "core", to = "leaf" } },
                },
                produced_by = new { vendor = "anthropic", model = "claude-opus-4-8" },
            },
            produced_at = Now,
        });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var found = await _fixture.Client.GetAsync(url);
        found.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await found.Content.ReadFromJsonAsync<JsonElement>();
        dto.GetProperty("head_sha").GetString().Should().Be("abc1234");
        var review = dto.GetProperty("review_recommendation");
        review.GetProperty("file_order")[0].GetProperty("path").GetString().Should().Be("src/Core.cs");
        review.GetProperty("file_order")[0].GetProperty("risk").GetString().Should().Be("high");
        review.GetProperty("affected_endpoints")[0].GetString().Should().Be("PUT /api/v1/x");
        review.GetProperty("module_graph").GetProperty("edges")[0].GetProperty("to").GetString().Should().Be("leaf");
        review.GetProperty("produced_by").GetProperty("model").GetString().Should().Be("claude-opus-4-8");
    }

    [Fact(DisplayName = "PUT review_recommendation отвергает невалидный risk-enum")]
    public async Task Put_rejects_invalid_risk_enum()
    {
        var intent = await RepositoriesApiSeed.IntentAsync(_fixture, Now);
        var binding = await RepositoriesApiSeed.ReadyBindingAsync(_fixture, intent.Id, 42, Now);
        var url = Artifact(binding.Id.Value, "review_recommendation");

        var put = await _fixture.Client.PutAsJsonAsync(url, new
        {
            render = "markdown",
            content = "# body",
            summary = "Review",
            source = "agent",
            source_refs = Array.Empty<string>(),
            review_recommendation = new
            {
                file_order = new[] { new { path = "a.cs", risk = "sky-high" } },
            },
            produced_at = Now,
        });

        put.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "PUT .../artifacts/{type} валидирует binding, PR attachment и type")]
    public async Task Put_validates_binding_pr_and_type()
    {
        var missing = await _fixture.Client.PutAsJsonAsync(
            Artifact("missing-binding", "static_analysis"),
            ValidArtifactBody());
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var intent = await RepositoriesApiSeed.IntentAsync(_fixture, Now);
        var binding = await RepositoriesApiSeed.ReadyBindingAsync(_fixture, intent.Id, null, Now);

        var withoutPr = await _fixture.Client.PutAsJsonAsync(
            Artifact(binding.Id.Value, "static_analysis"),
            ValidArtifactBody());
        withoutPr.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var secondIntent = await RepositoriesApiSeed.IntentAsync(_fixture, Now);
        var prBinding = await RepositoriesApiSeed.ReadyBindingAsync(_fixture, secondIntent.Id, 42, Now);
        var badType = await _fixture.Client.PutAsJsonAsync(
            Artifact(prBinding.Id.Value, "Bad Type"),
            ValidArtifactBody());
        badType.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private static Uri Artifacts(string bindingId) =>
        new($"/api/v1/repositories/{bindingId}/artifacts", UriKind.Relative);

    private static Uri Artifact(string bindingId, string type) =>
        new($"/api/v1/repositories/{bindingId}/artifacts/{type}", UriKind.Relative);

    private static object ValidArtifactBody() => new
    {
        render = "markdown",
        content = "# body",
        summary = "Summary",
        source = "static",
        source_refs = Array.Empty<string>(),
        produced_at = Now,
    };
}
