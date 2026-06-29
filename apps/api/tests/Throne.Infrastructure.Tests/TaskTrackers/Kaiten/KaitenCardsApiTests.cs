using System.Globalization;
using System.Net;
using FluentAssertions;
using Throne.Infrastructure.TaskTrackers.Kaiten;
using Throne.Infrastructure.TaskTrackers.Kaiten.Models;

namespace Throne.Infrastructure.Tests.TaskTrackers.Kaiten;

public class KaitenCardsApiTests
{
    private const string CardJson = """
    {
      "id": 42,
      "title": "Fix login",
      "description": "broken",
      "board_id": 7,
      "column_id": 3,
      "lane_id": 9,
      "tag_ids": [1, 2, 3],
      "condition": 1,
      "created": "2026-01-01T10:00:00.000Z",
      "updated": "2026-02-02T12:00:00.000Z",
      "column_changed_at": "2026-02-01T08:00:00.000Z",
      "type_id": 5,
      "version": 17
    }
    """;

    [Fact(DisplayName = "Card: snake_case поля маппятся, включая tag_ids, column_changed_at и version")]
    public async Task Maps_card_fields()
    {
        var (executor, handler) = KaitenTestHarness.NewExecutor();
        handler.Enqueue(HttpStatusCode.OK, CardJson);
        var cards = new KaitenCardsApi(executor);

        var card = await cards.GetCardAsync(KaitenTestHarness.Connection, 42, CancellationToken.None);

        card.Id.Should().Be(42);
        card.BoardId.Should().Be(7);
        card.ColumnId.Should().Be(3);
        card.LaneId.Should().Be(9);
        card.TagIds.Should().Equal(1, 2, 3);
        card.Condition.Should().Be(1);
        card.TypeId.Should().Be(5);
        card.ColumnChangedAt.Should().Be(new DateTimeOffset(2026, 2, 1, 8, 0, 0, TimeSpan.Zero));
        card.Updated.Should().Be(new DateTimeOffset(2026, 2, 2, 12, 0, 0, TimeSpan.Zero));
        card.Version.Should().Be(17);
    }

    [Fact(DisplayName = "List-row Kaiten опускает description, но несёт version → Description=null, Version наполнен")]
    public async Task List_row_omits_description_but_carries_version()
    {
        var (executor, handler) = KaitenTestHarness.NewExecutor();
        const string listJson = """
        [
          {
            "id": 42,
            "title": "Fix login",
            "board_id": 7,
            "column_id": 3,
            "condition": 1,
            "description_filled": true,
            "version": 17
          }
        ]
        """;
        handler.Enqueue(HttpStatusCode.OK, listJson);
        var cards = new KaitenCardsApi(executor);

        var list = await cards.ListCardsAsync(
            KaitenTestHarness.Connection,
            new KaitenCardQuery(BoardId: 7),
            CancellationToken.None);

        list.Should().HaveCount(1);
        list[0].Description.Should().BeNull();
        list[0].Version.Should().Be(17);
    }

    [Fact(DisplayName = "ListCards: фильтры идут query-параметрами")]
    public async Task List_cards_sends_query_filters()
    {
        var (executor, handler) = KaitenTestHarness.NewExecutor();
        handler.Enqueue(HttpStatusCode.OK, "[]");
        var cards = new KaitenCardsApi(executor);

        await cards.ListCardsAsync(
            KaitenTestHarness.Connection,
            new KaitenCardQuery(BoardId: 7, Condition: 1, Limit: 50),
            CancellationToken.None);

        handler.Requests.Single().Uri.Query.Should().Be("?board_id=7&condition=1&limit=50");
    }

    [Fact(DisplayName = "ListAllCards: листает страницы, пока страница полная, и склеивает")]
    public async Task List_all_cards_paginates_until_partial_page()
    {
        var (executor, handler) = KaitenTestHarness.NewExecutor();
        handler.Enqueue(HttpStatusCode.OK, Page(1, 2)).Enqueue(HttpStatusCode.OK, Page(3, 1));
        var cards = new KaitenCardsApi(executor);

        var all = await cards.ListAllCardsAsync(
            KaitenTestHarness.Connection,
            new KaitenCardQuery(BoardId: 7, Limit: 2),
            CancellationToken.None);

        all.Select(c => c.Id).Should().Equal(1, 2, 3);
        handler.Requests.Should().HaveCount(2);
        handler.Requests[0].Uri.Query.Should().Contain("offset=0");
        handler.Requests[1].Uri.Query.Should().Contain("offset=2");
    }

    private static string Page(int firstId, int count)
    {
        var items = Enumerable.Range(firstId, count).Select(id =>
            $$"""{"id":{{id.ToString(CultureInfo.InvariantCulture)}},"title":"c","board_id":7,"column_id":3,"condition":1}""");
        return "[" + string.Join(",", items) + "]";
    }
}
