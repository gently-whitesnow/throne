using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Tests.Infrastructure;
using Throne.Application.TaskTrackers;

namespace Throne.Api.Tests.TaskTrackers;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class IntentCardAttachmentsControllerTests(SqliteFixture sqlite) : IAsyncLifetime, IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);

    private CardAttachmentsApiFixture _fixture = null!;

    public Task InitializeAsync()
    {
        _fixture = new CardAttachmentsApiFixture(sqlite);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    void IDisposable.Dispose() { /* IAsyncLifetime.DisposeAsync owns cleanup */ }

    [Fact(DisplayName = "GET cards возвращает 200 с пустым списком когда attachments нет")]
    public async Task List_empty()
    {
        var intent = await _fixture.SeedIntentAsync(Now);

        var response = await _fixture.Client.GetAsync(CardsUri(intent.Id.Value));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        items.Should().NotBeNull().And.BeEmpty();
    }

    [Fact(DisplayName = "GET cards возвращает 404 для несуществующего intent")]
    public async Task List_404_when_intent_missing()
    {
        var response = await _fixture.Client.GetAsync(CardsUri("missing"));
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST cards — happy path: 200 + available attachment со снапшотом")]
    public async Task Attach_happy_path()
    {
        var intent = await _fixture.SeedIntentAsync(Now);
        await _fixture.SeedConnectionAsync();
        _fixture.Provider.OnGetCard = id => Task.FromResult<TaskTrackerCard?>(
            StubCardTrackerProvider.Card(id, "Fresh card"));

        var response = await _fixture.Client.PostAsJsonAsync(
            CardsUri(intent.Id.Value), new { tracker = "kaiten", board_id = "10", card_id = "42" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>();
        dto.GetProperty("title").GetString().Should().Be("Fresh card");
        dto.GetProperty("availability").GetString().Should().Be("available");
        dto.GetProperty("card_id").GetString().Should().Be("42");
        dto.GetProperty("web_url").GetString().Should().Be("https://acme.kaiten.ru/42");
    }

    [Fact(DisplayName = "GET cards → web_url = null для attachment без сохранённого коннекта")]
    public async Task List_web_url_null_when_connection_missing()
    {
        var intent = await _fixture.SeedIntentAsync(Now);
        await _fixture.SeedConnectionAsync();
        _fixture.Provider.OnGetCard = id => Task.FromResult<TaskTrackerCard?>(
            StubCardTrackerProvider.Card(id, "Attached"));
        await AttachAndGetIdAsync(intent.Id.Value);
        await RemoveConnectionAsync();

        var response = await _fixture.Client.GetAsync(CardsUri(intent.Id.Value));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        items.Should().ContainSingle();
        items![0].GetProperty("web_url").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact(DisplayName = "POST cards на пропавшую карточку → 404 card_attachment.card_not_found")]
    public async Task Attach_card_gone_404()
    {
        var intent = await _fixture.SeedIntentAsync(Now);
        await _fixture.SeedConnectionAsync();
        _fixture.Provider.OnGetCard = _ => Task.FromResult<TaskTrackerCard?>(null);

        var response = await _fixture.Client.PostAsJsonAsync(
            CardsUri(intent.Id.Value), new { tracker = "kaiten", board_id = "10", card_id = "42" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "POST cards без коннекта → 409 card_attachment.tracker_not_connected")]
    public async Task Attach_not_connected_409()
    {
        var intent = await _fixture.SeedIntentAsync(Now);

        var response = await _fixture.Client.PostAsJsonAsync(
            CardsUri(intent.Id.Value), new { tracker = "kaiten", board_id = "10", card_id = "42" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "POST cards на неподдерживаемый трекер → 422")]
    public async Task Attach_unsupported_tracker_422()
    {
        var intent = await _fixture.SeedIntentAsync(Now);

        var response = await _fixture.Client.PostAsJsonAsync(
            CardsUri(intent.Id.Value), new { tracker = "jira", board_id = "10", card_id = "42" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST cards при броске провайдера → 502")]
    public async Task Attach_provider_throws_502()
    {
        var intent = await _fixture.SeedIntentAsync(Now);
        await _fixture.SeedConnectionAsync();
        _fixture.Provider.OnGetCard = _ => throw new InvalidOperationException("boom");

        var response = await _fixture.Client.PostAsJsonAsync(
            CardsUri(intent.Id.Value), new { tracker = "kaiten", board_id = "10", card_id = "42" });

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }

    [Fact(DisplayName = "POST refresh деградирует в unavailable без коннекта (200)")]
    public async Task Refresh_degrades_unavailable()
    {
        var intent = await _fixture.SeedIntentAsync(Now);
        await _fixture.SeedConnectionAsync();
        _fixture.Provider.OnGetCard = id => Task.FromResult<TaskTrackerCard?>(
            StubCardTrackerProvider.Card(id, "Attached"));
        var attachId = await AttachAndGetIdAsync(intent.Id.Value);

        await RemoveConnectionAsync();
        var response = await _fixture.Client.PostAsync(RefreshUri(intent.Id.Value, attachId), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>();
        dto.GetProperty("availability").GetString().Should().Be("unavailable");
        dto.GetProperty("title").GetString().Should().Be("Attached");
    }

    [Fact(DisplayName = "DELETE cards возвращает 204 идемпотентно (существующий и отсутствующий)")]
    public async Task Detach_204_idempotent()
    {
        var intent = await _fixture.SeedIntentAsync(Now);
        await _fixture.SeedConnectionAsync();
        _fixture.Provider.OnGetCard = id => Task.FromResult<TaskTrackerCard?>(
            StubCardTrackerProvider.Card(id, "Attached"));
        var attachId = await AttachAndGetIdAsync(intent.Id.Value);

        var first = await _fixture.Client.DeleteAsync(DetachUri(intent.Id.Value, attachId));
        var second = await _fixture.Client.DeleteAsync(DetachUri(intent.Id.Value, attachId));

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<string> AttachAndGetIdAsync(string intentId)
    {
        var response = await _fixture.Client.PostAsJsonAsync(
            CardsUri(intentId), new { tracker = "kaiten", board_id = "10", card_id = "42" });
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>();
        return dto.GetProperty("id").GetString()!;
    }

    private async Task RemoveConnectionAsync()
    {
        await using var scope = _fixture.Services.CreateAsyncScope();
        var store = scope.ServiceProvider
            .GetRequiredService<Throne.Application.Ports.ITaskTrackerConnectionStore>();
        await store.DeleteAsync("kaiten", CancellationToken.None);
    }

    private static Uri CardsUri(string intentId) =>
        new($"/api/v1/intents/{intentId}/task-tracker/cards", UriKind.Relative);

    private static Uri RefreshUri(string intentId, string attachmentId) =>
        new($"/api/v1/intents/{intentId}/task-tracker/cards/{attachmentId}/refresh", UriKind.Relative);

    private static Uri DetachUri(string intentId, string attachmentId) =>
        new($"/api/v1/intents/{intentId}/task-tracker/cards/{attachmentId}", UriKind.Relative);
}
