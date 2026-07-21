using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Throne.Api.Tests.Infrastructure;
using Throne.Application.TaskTrackers;

namespace Throne.Api.Tests.TaskTrackers;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public sealed class BoardCardBrowserEndpointTests(SqliteFixture sqlite) : IAsyncLifetime, IDisposable
{
    private CardAttachmentsApiFixture _fixture = null!;

    public Task InitializeAsync()
    {
        _fixture = new CardAttachmentsApiFixture(sqlite);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _fixture.DisposeAsync();

    void IDisposable.Dispose() { /* IAsyncLifetime.DisposeAsync owns cleanup */ }

    [Fact(DisplayName = "GET board cards → 200 со списком активных карточек")]
    public async Task List_ok()
    {
        await _fixture.SeedConnectionAsync();
        _fixture.Provider.OnListBoardCards = _ => Task.FromResult<IReadOnlyList<TaskTrackerCard>>(
            [StubCardTrackerProvider.Card("42", "First"), StubCardTrackerProvider.Card("43", "Second")]);

        var response = await _fixture.Client.GetAsync(CardsUri("kaiten", "10"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var cards = body.GetProperty("cards");
        cards.GetArrayLength().Should().Be(2);
        cards[0].GetProperty("card_id").GetString().Should().Be("42");
        cards[0].GetProperty("text").GetString().Should().Be("First\n\nbody");
        cards[0].GetProperty("web_url").GetString().Should().Be("https://acme.kaiten.ru/42");
        cards[1].GetProperty("web_url").GetString().Should().Be("https://acme.kaiten.ru/43");
    }


    [Fact(DisplayName = "GET board cards → 409 без сохранённого коннекта")]
    public async Task List_not_connected_409()
    {
        var response = await _fixture.Client.GetAsync(CardsUri("kaiten", "10"));
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "GET board cards → 422 для неподдерживаемого трекера")]
    public async Task List_unsupported_422()
    {
        var response = await _fixture.Client.GetAsync(CardsUri("jira", "10"));
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "GET board cards → 402 при tariff-стене (Blocked)")]
    public async Task List_blocked_402()
    {
        await _fixture.SeedConnectionAsync();
        _fixture.Provider.OnListBoardCards = _ =>
            throw new TaskTrackerConnectionException(TaskTrackerConnectionHealth.Blocked, "tariff");

        var response = await _fixture.Client.GetAsync(CardsUri("kaiten", "10"));

        response.StatusCode.Should().Be(HttpStatusCode.PaymentRequired);
    }

    [Fact(DisplayName = "GET board cards → 409 при отозванном токене (Auth)")]
    public async Task List_auth_409()
    {
        await _fixture.SeedConnectionAsync();
        _fixture.Provider.OnListBoardCards = _ =>
            throw new TaskTrackerConnectionException(TaskTrackerConnectionHealth.Auth, "revoked");

        var response = await _fixture.Client.GetAsync(CardsUri("kaiten", "10"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact(DisplayName = "GET board cards → 502 когда трекер недостижим (Offline)")]
    public async Task List_offline_502()
    {
        await _fixture.SeedConnectionAsync();
        _fixture.Provider.OnListBoardCards = _ =>
            throw new TaskTrackerConnectionException(TaskTrackerConnectionHealth.Offline, "down");

        var response = await _fixture.Client.GetAsync(CardsUri("kaiten", "10"));

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
    }

    [Fact(DisplayName = "GET single card → 200 с полным text")]
    public async Task Get_card_ok()
    {
        await _fixture.SeedConnectionAsync();
        _fixture.Provider.OnGetCard = id => Task.FromResult<TaskTrackerCard?>(
            StubCardTrackerProvider.Card(id, "Detailed"));

        var response = await _fixture.Client.GetAsync(CardUri("kaiten", "10", "42"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>();
        dto.GetProperty("card_id").GetString().Should().Be("42");
        dto.GetProperty("text").GetString().Should().Be("Detailed\n\nbody");
        dto.GetProperty("web_url").GetString().Should().Be("https://acme.kaiten.ru/42");
    }

    [Fact(DisplayName = "GET single card → 404 когда карточка пропала")]
    public async Task Get_card_gone_404()
    {
        await _fixture.SeedConnectionAsync();
        _fixture.Provider.OnGetCard = _ => Task.FromResult<TaskTrackerCard?>(null);

        var response = await _fixture.Client.GetAsync(CardUri("kaiten", "10", "42"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "GET single card → 404 когда карточка на другой доске")]
    public async Task Get_card_wrong_board_404()
    {
        await _fixture.SeedConnectionAsync();
        _fixture.Provider.OnGetCard = id => Task.FromResult<TaskTrackerCard?>(
            StubCardTrackerProvider.Card(id, "Elsewhere"));

        var response = await _fixture.Client.GetAsync(CardUri("kaiten", "999", "42"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static Uri CardsUri(string tracker, string board) =>
        new($"/api/v1/task-trackers/{tracker}/boards/{board}/cards", UriKind.Relative);

    private static Uri CardUri(string tracker, string board, string card) =>
        new($"/api/v1/task-trackers/{tracker}/boards/{board}/cards/{card}", UriKind.Relative);
}
