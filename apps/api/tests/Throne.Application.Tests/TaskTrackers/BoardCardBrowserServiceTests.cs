using FluentAssertions;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Application.TaskTrackers;

namespace Throne.Application.Tests.TaskTrackers;

public sealed class BoardCardBrowserServiceTests
{
    private const string Tracker = "kaiten";

    [Fact(DisplayName = "ListBoardCards → returns the provider cards and records Connected health")]
    public async Task ListBoardCards_returns_cards()
    {
        var provider = new StubProvider
        {
            OnListBoardCards = _ => Task.FromResult<IReadOnlyList<TaskTrackerCard>>([Card("1")]),
        };
        var store = new StubStore(Connected());
        var service = Service(provider, store);

        var cards = await service.ListBoardCardsAsync(Tracker, "10", CancellationToken.None);

        cards.Should().ContainSingle().Which.CardId.Should().Be("1");
        store.LastHealth.Should().Be(TaskTrackerConnectionHealth.Connected);
    }

    [Fact(DisplayName = "ListBoardCards → unknown tracker is provider_unsupported (422)")]
    public async Task ListBoardCards_unknown_tracker()
    {
        var service = Service(new StubProvider(), new StubStore(Connected()));

        var act = () => service.ListBoardCardsAsync("jira", "10", CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>())
            .Which.Code.Should().Be(ErrorCodes.TaskTrackerProviderUnsupported);
    }

    [Fact(DisplayName = "ListBoardCards → no saved connection is connection_missing (409)")]
    public async Task ListBoardCards_not_connected()
    {
        var service = Service(new StubProvider(), new StubStore(null));

        var act = () => service.ListBoardCardsAsync(Tracker, "10", CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>())
            .Which.Code.Should().Be(ErrorCodes.TaskTrackerConnectionMissing);
    }

    [Theory(DisplayName = "ListBoardCards → maps connection health onto the degradation surface + persists it")]
    [InlineData(TaskTrackerConnectionHealth.Auth, ErrorCodes.TaskTrackerConnectionRejected)]
    [InlineData(TaskTrackerConnectionHealth.Blocked, ErrorCodes.TaskTrackerConnectionBlocked)]
    [InlineData(TaskTrackerConnectionHealth.Offline, ErrorCodes.TaskTrackerUpstreamUnavailable)]
    public async Task ListBoardCards_maps_health(TaskTrackerConnectionHealth health, string expectedCode)
    {
        var provider = new StubProvider
        {
            OnListBoardCards = _ => throw new TaskTrackerConnectionException(health, "boom"),
        };
        var store = new StubStore(Connected());
        var service = Service(provider, store);

        var act = () => service.ListBoardCardsAsync(Tracker, "10", CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>()).Which.Code.Should().Be(expectedCode);
        store.LastHealth.Should().Be(health);
    }

    [Fact(DisplayName = "GetBoardCard → returns the card on the requested board")]
    public async Task GetBoardCard_returns_card()
    {
        var provider = new StubProvider { OnGetCard = _ => Task.FromResult<TaskTrackerCard?>(Card("7")) };
        var service = Service(provider, new StubStore(Connected()));

        var card = await service.GetBoardCardAsync(Tracker, "10", "7", CancellationToken.None);

        card.CardId.Should().Be("7");
    }

    [Fact(DisplayName = "GetBoardCard → gone card is card_not_found (404)")]
    public async Task GetBoardCard_gone()
    {
        var provider = new StubProvider { OnGetCard = _ => Task.FromResult<TaskTrackerCard?>(null) };
        var service = Service(provider, new StubStore(Connected()));

        var act = () => service.GetBoardCardAsync(Tracker, "10", "7", CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>())
            .Which.Code.Should().Be(ErrorCodes.CardAttachmentCardNotFound);
    }

    [Fact(DisplayName = "GetBoardCard → a card on another board is card_not_found (404)")]
    public async Task GetBoardCard_wrong_board()
    {
        var provider = new StubProvider { OnGetCard = _ => Task.FromResult<TaskTrackerCard?>(Card("7", boardId: "99")) };
        var service = Service(provider, new StubStore(Connected()));

        var act = () => service.GetBoardCardAsync(Tracker, "10", "7", CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>())
            .Which.Code.Should().Be(ErrorCodes.CardAttachmentCardNotFound);
    }

    private static BoardCardBrowserService Service(StubProvider provider, StubStore store) =>
        new(new TaskTrackerProviderRegistry([provider]), store, TimeProvider.System);

    private static TaskTrackerStoredConnection Connected() =>
        new("https://acme.kaiten.ru", "tok", []);

    private static TaskTrackerCard Card(string cardId, string boardId = "10") =>
        new(cardId, boardId, "100", "In Progress", $"card-{cardId}", "body", null, null, Archived: false, "v1");

    private sealed class StubProvider : ITaskTrackerConnectionProvider
    {
        public string TrackerKey => Tracker;

        public string DisplayName => "Kaiten";

        public Func<string, Task<IReadOnlyList<TaskTrackerCard>>> OnListBoardCards { get; set; } =
            _ => Task.FromResult<IReadOnlyList<TaskTrackerCard>>([]);

        public Func<string, string?, int, Task<IReadOnlyList<TaskTrackerCard>>> OnSearchCards { get; set; } =
            (_, _, _) => Task.FromResult<IReadOnlyList<TaskTrackerCard>>([]);

        public Func<string, Task<TaskTrackerCard?>> OnGetCard { get; set; } =
            _ => Task.FromResult<TaskTrackerCard?>(null);

        public Task<TaskTrackerProbeResult> ProbeAsync(TaskTrackerConnectionDescriptor connection, CancellationToken ct) =>
            Task.FromResult(TaskTrackerProbeResult.Connected());

        public Task<IReadOnlyList<TaskTrackerSpaceTopology>> ListBoardsAsync(
            TaskTrackerConnectionDescriptor connection, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TaskTrackerSpaceTopology>>([]);

        public Task<IReadOnlyList<TaskTrackerCard>> ListBoardCardsAsync(
            TaskTrackerConnectionDescriptor connection, string boardId, CancellationToken ct) =>
            OnListBoardCards(boardId);

        public Task<IReadOnlyList<TaskTrackerCard>> SearchCardsAsync(
            TaskTrackerConnectionDescriptor connection, string boardId, string? query, int limit, CancellationToken ct) =>
            OnSearchCards(boardId, query, limit);

        public Task<TaskTrackerCard?> GetCardAsync(
            TaskTrackerConnectionDescriptor connection, string cardId, CancellationToken ct) =>
            OnGetCard(cardId);

        public string? BuildCardWebUrl(TaskTrackerConnectionDescriptor connection, string cardId) =>
            $"{connection.BaseUrl.TrimEnd('/')}/{cardId}";
    }

    private sealed class StubStore(TaskTrackerStoredConnection? stored) : ITaskTrackerConnectionStore
    {
        public TaskTrackerConnectionHealth? LastHealth { get; private set; }

        public Task<TaskTrackerStoredConnection?> GetAsync(string tracker, CancellationToken ct) =>
            Task.FromResult(stored);

        public Task SaveConnectionAsync(string tracker, string baseUrl, string token, CancellationToken ct) =>
            Task.CompletedTask;

        public Task DeleteAsync(string tracker, CancellationToken ct) => Task.CompletedTask;

        public Task SaveHealthAsync(
            string tracker, TaskTrackerConnectionHealth status, string? detail, DateTimeOffset checkedAt, CancellationToken ct)
        {
            LastHealth = status;
            return Task.CompletedTask;
        }

        public Task SaveSelectionAsync(
            string tracker, IReadOnlyList<TaskTrackerBoardSelection> selection, CancellationToken ct) =>
            Task.CompletedTask;
    }
}
