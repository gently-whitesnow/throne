using System.Net;
using FluentAssertions;
using Throne.Application.Errors;
using Throne.Application.TaskTrackers;
using static Throne.Infrastructure.Tests.TaskTrackers.GenericHttp.GenericHttpTestHarness;

namespace Throne.Infrastructure.Tests.TaskTrackers.GenericHttp;

public sealed class GenericHttpTaskTrackerProviderTests
{
    [Fact(DisplayName = "Provider identity is the generic custom-http tracker key")]
    public void Provider_identity()
    {
        var (provider, _) = NewProvider();

        provider.TrackerKey.Should().Be("custom-http");
        provider.DisplayName.Should().Be("Custom HTTP");
    }

    [Fact(DisplayName = "Probe uses bearer auth and maps success to Connected")]
    public async Task Probe_connected()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(HttpStatusCode.OK, "{}");

        var result = await provider.ProbeAsync(Descriptor, CancellationToken.None);

        result.Health.Should().Be(TaskTrackerConnectionHealth.Connected);
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Uri.ToString().Should().Be("https://tasks.example.test/api/task-tracker/health");
        handler.Requests[0].Authorization.Should().Be("Bearer secret-token");
    }

    [Theory(DisplayName = "Probe classifies auth, blocked, and offline failures")]
    [InlineData(HttpStatusCode.Unauthorized, TaskTrackerConnectionHealth.Auth)]
    [InlineData(HttpStatusCode.Forbidden, TaskTrackerConnectionHealth.Auth)]
    [InlineData(HttpStatusCode.PaymentRequired, TaskTrackerConnectionHealth.Blocked)]
    [InlineData(HttpStatusCode.BadGateway, TaskTrackerConnectionHealth.Offline)]
    public async Task Probe_classifies_failures(HttpStatusCode status, TaskTrackerConnectionHealth expected)
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(status, "{}");

        var result = await provider.ProbeAsync(Descriptor, CancellationToken.None);

        result.Health.Should().Be(expected);
    }

    [Fact(DisplayName = "ListBoards maps generic facets into a single Custom HTTP topology space")]
    public async Task ListBoards_maps_topology()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(HttpStatusCode.OK, """
        {"boards":[{"board_id":"coding","title":"Coding tasks"}]}
        """);

        var topology = await provider.ListBoardsAsync(Descriptor, CancellationToken.None);

        topology.Should().ContainSingle();
        topology[0].SpaceId.Should().Be("custom-http");
        topology[0].Boards.Should().ContainSingle();
        topology[0].Boards[0].BoardId.Should().Be("coding");
        topology[0].Boards[0].BoardTitle.Should().Be("Coding tasks");
    }

    [Fact(DisplayName = "ListBoards translates auth failure into connection-rejected")]
    public async Task ListBoards_auth_failure()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(HttpStatusCode.Forbidden, "{}");

        var act = () => provider.ListBoardsAsync(Descriptor, CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>())
            .Which.Code.Should().Be(ErrorCodes.TaskTrackerConnectionRejected);
    }

    [Fact(DisplayName = "ListBoardCards maps cards and excludes archived rows defensively")]
    public async Task ListBoardCards_maps_cards()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(HttpStatusCode.OK, """
        {"cards":[
          {"card_id":"1","board_id":"coding","title":"Active","description":"body","updated_at":"2026-07-16T10:00:00Z","archived":false,"card_version":"v1","web_url":"https://tasks/ui/1"},
          {"card_id":"2","board_id":"coding","title":"Closed","archived":true}
        ]}
        """);

        var cards = await provider.ListBoardCardsAsync(Descriptor, "coding", CancellationToken.None);

        cards.Should().ContainSingle();
        cards[0].CardId.Should().Be("1");
        cards[0].BoardId.Should().Be("coding");
        cards[0].Description.Should().Be("body");
        cards[0].RevisionTag.Should().Be("v1");
        cards[0].WebUrl.Should().Be("https://tasks/ui/1");
        handler.Requests[0].Uri.PathAndQuery.Should().Be("/api/task-tracker/boards/coding/cards");
    }

    [Fact(DisplayName = "SearchCards forwards query and limit")]
    public async Task SearchCards_forwards_query_and_limit()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(HttpStatusCode.OK, """{"cards":[]}""");

        _ = await provider.SearchCardsAsync(
            Descriptor, "coding", "parser bug", limit: 7, CancellationToken.None);

        handler.Requests[0].Uri.PathAndQuery.Should()
            .Be("/api/task-tracker/boards/coding/cards/search?query=parser%20bug&limit=7");
    }

    [Fact(DisplayName = "GetCard returns null only on 404")]
    public async Task GetCard_gone_returns_null()
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(HttpStatusCode.NotFound, "{}");

        var card = await provider.GetCardAsync(Descriptor, "missing", CancellationToken.None);

        card.Should().BeNull();
        handler.Requests[0].Uri.PathAndQuery.Should().Be("/api/task-tracker/cards/missing");
    }

    [Theory(DisplayName = "Card reads classify upstream failures per ADR-0053")]
    [InlineData(HttpStatusCode.Unauthorized, TaskTrackerConnectionHealth.Auth)]
    [InlineData(HttpStatusCode.Forbidden, TaskTrackerConnectionHealth.Auth)]
    [InlineData(HttpStatusCode.PaymentRequired, TaskTrackerConnectionHealth.Blocked)]
    [InlineData(HttpStatusCode.BadGateway, TaskTrackerConnectionHealth.Offline)]
    public async Task Card_reads_classify_failures(HttpStatusCode status, TaskTrackerConnectionHealth expected)
    {
        var (provider, handler) = NewProvider();
        handler.Enqueue(status, "{}");

        var act = () => provider.ListBoardCardsAsync(Descriptor, "coding", CancellationToken.None);

        (await act.Should().ThrowAsync<TaskTrackerConnectionException>())
            .Which.Health.Should().Be(expected);
    }
}
