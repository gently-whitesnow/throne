using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Throne.Api.Tests.Infrastructure;

namespace Throne.Api.Tests.Repositories;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class RepositoriesControllerTests(MongoFixture mongo) : IAsyncLifetime, IDisposable
{
    private RepositoriesApiFixture _fixture = null!;

    public Task InitializeAsync()
    {
        _fixture = new RepositoriesApiFixture(mongo, TestGitProvider.Create());
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    void IDisposable.Dispose() { /* IAsyncLifetime.DisposeAsync owns cleanup */ }

    [Fact(DisplayName = "POST /api/v1/repositories регистрирует координату (201), повтор идемпотентен (200)")]
    public async Task Create_is_idempotent()
    {
        var body = new { provider = "github", owner = "octo", repo = "hello" };

        var first = await _fixture.Client.PostAsJsonAsync(Repositories(), body);
        first.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await first.Content.ReadFromJsonAsync<JsonElement>();
        dto.GetProperty("full_name").GetString().Should().Be("octo/hello");
        dto.GetProperty("provider").GetString().Should().Be("github");

        var second = await _fixture.Client.PostAsJsonAsync(Repositories(), body);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "GET /api/v1/repositories отдаёт зарегистрированные координаты (метаданные, без страниц)")]
    public async Task List_includes_registered()
    {
        await _fixture.Client.PostAsJsonAsync(Repositories(), new { provider = "github", owner = "octo", repo = "hello" });

        var response = await _fixture.Client.GetAsync(Repositories());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        items.Should().ContainSingle(i => i.GetProperty("full_name").GetString() == "octo/hello");
    }

    [Fact(DisplayName = "GET /api/v1/repositories/{coord} — 200 для известного, 404 для незарегистрированного")]
    public async Task Get_200_and_404()
    {
        await _fixture.Client.PostAsJsonAsync(Repositories(), new { provider = "github", owner = "octo", repo = "hello" });

        var found = await _fixture.Client.GetAsync(Repository("github", "octo", "hello"));
        found.StatusCode.Should().Be(HttpStatusCode.OK);

        var missing = await _fixture.Client.GetAsync(Repository("github", "octo", "unknown"));
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "GET /api/v1/repositories/{coord} с невалидным owner даёт 422")]
    public async Task Get_invalid_coordinate_422()
    {
        var response = await _fixture.Client.GetAsync(Repository("github", "_bad", "hello"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("repository.coordinate_invalid");
    }

    [Fact(DisplayName = "PUT .../documents/{slug} создаёт версию 1, апдейт по expected_version даёт 2, рассинхрон — 409")]
    public async Task Put_creates_updates_and_conflicts()
    {
        var url = Document("github", "octo", "hello", "db-schema-map");

        var created = await _fixture.Client.PutAsJsonAsync(url, new { title = "Schema", document = "# v1" });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdDto = await created.Content.ReadFromJsonAsync<JsonElement>();
        createdDto.GetProperty("version").GetInt32().Should().Be(1);
        createdDto.GetProperty("document").GetString().Should().Be("# v1");

        var updated = await _fixture.Client.PutAsJsonAsync(url, new { title = "Schema", document = "# v2", expected_version = 1 });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await updated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetInt32().Should().Be(2);

        var stale = await _fixture.Client.PutAsJsonAsync(url, new { title = "Schema", document = "# stale", expected_version = 1 });
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await stale.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("repository_artifact.version_conflict");
        problem.GetProperty("current_version").GetInt32().Should().Be(2);
    }

    [Fact(DisplayName = "PUT .../documents/{slug} с невалидным slug даёт 422")]
    public async Task Put_invalid_slug_422()
    {
        var response = await _fixture.Client.PutAsJsonAsync(
            Document("github", "octo", "hello", "Bad_Slug"),
            new { title = "Schema", document = "# x" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "GET .../documents/{slug} — 200 с телом, 404 для отсутствующей страницы")]
    public async Task Get_document_200_and_404()
    {
        await _fixture.Client.PutAsJsonAsync(
            Document("github", "octo", "hello", "db-schema-map"),
            new { title = "Schema", document = "# body" });

        var found = await _fixture.Client.GetAsync(Document("github", "octo", "hello", "db-schema-map"));
        found.StatusCode.Should().Be(HttpStatusCode.OK);
        (await found.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("document").GetString().Should().Be("# body");

        var missing = await _fixture.Client.GetAsync(Document("github", "octo", "hello", "no-such-page"));
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "GET .../documents отдаёт сводки страниц без markdown-тела")]
    public async Task List_documents_omits_body()
    {
        await _fixture.Client.PutAsJsonAsync(
            Document("github", "octo", "hello", "db-schema-map"),
            new { title = "Schema", document = "# body" });

        var response = await _fixture.Client.GetAsync(Documents("github", "octo", "hello"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        var page = items.Should().ContainSingle().Subject;
        page.GetProperty("slug").GetString().Should().Be("db-schema-map");
        page.GetProperty("version").GetInt32().Should().Be(1);
        page.TryGetProperty("document", out _).Should().BeFalse("the list projection must not ship the markdown body");
    }

    [Fact(DisplayName = "GET .../documents/{slug}/versions отдаёт таймлайн по возрастанию, 404 для отсутствующей страницы")]
    public async Task List_versions_timeline_and_404()
    {
        var url = Document("github", "octo", "hello", "db-schema-map");
        await _fixture.Client.PutAsJsonAsync(url, new { title = "Schema", document = "# v1" });
        await _fixture.Client.PutAsJsonAsync(url, new { title = "Schema", document = "# v2", expected_version = 1 });

        var response = await _fixture.Client.GetAsync(Versions("github", "octo", "hello", "db-schema-map"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var versions = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        versions.Should().NotBeNull();
        versions!.Select(v => v.GetProperty("version").GetInt32()).Should().Equal(1, 2);
        versions![0].GetProperty("document").GetString().Should().Be("# v1");

        var missing = await _fixture.Client.GetAsync(Versions("github", "octo", "hello", "no-such-page"));
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static Uri Repositories() => new("/api/v1/repositories", UriKind.Relative);

    private static Uri Repository(string provider, string owner, string repo) =>
        new($"/api/v1/repositories/{provider}/{owner}/{repo}", UriKind.Relative);

    private static Uri Documents(string provider, string owner, string repo) =>
        new($"/api/v1/repositories/{provider}/{owner}/{repo}/documents", UriKind.Relative);

    private static Uri Document(string provider, string owner, string repo, string slug) =>
        new($"/api/v1/repositories/{provider}/{owner}/{repo}/documents/{slug}", UriKind.Relative);

    private static Uri Versions(string provider, string owner, string repo, string slug) =>
        new($"/api/v1/repositories/{provider}/{owner}/{repo}/documents/{slug}/versions", UriKind.Relative);
}
