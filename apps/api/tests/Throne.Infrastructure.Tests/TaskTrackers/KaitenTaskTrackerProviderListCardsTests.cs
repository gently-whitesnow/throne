using System.Net;
using FluentAssertions;
using Throne.Application.TaskTrackers;
using Throne.Infrastructure.TaskTrackers;
using Throne.Infrastructure.TaskTrackers.Kaiten;
using Throne.Infrastructure.TaskTrackers.Kaiten.Models;
using static Throne.Infrastructure.Tests.TaskTrackers.KaitenProviderTestHarness;

namespace Throne.Infrastructure.Tests.TaskTrackers;

public sealed class KaitenTaskTrackerProviderListCardsTests
{
    [Fact(DisplayName = "ListBoardCards → queries live cards for the board and resolves column titles")]
    public async Task ListBoardCards_projects_cards()
    {
        KaitenCardQuery? seen = null;
        var provider = Provider(
            listCards: (_, query, _) =>
            {
                seen = query;
                return Task.FromResult<IReadOnlyList<KaitenCard>>([SampleCard(columnId: 100)]);
            },
            columns: (_, _, _) => Task.FromResult<IReadOnlyList<KaitenColumn>>(
                [new KaitenColumn(100, "In Progress", 10, 0)]));

        var cards = await provider.ListBoardCardsAsync(Descriptor, "10", CancellationToken.None);

        cards.Should().HaveCount(1);
        cards[0].CardId.Should().Be("42");
        cards[0].BoardId.Should().Be("10");
        cards[0].ColumnTitle.Should().Be("In Progress");
        cards[0].Archived.Should().BeFalse();
        seen.Should().NotBeNull();
        seen!.BoardId.Should().Be(10);
        seen.Condition.Should().Be(KaitenCardConditions.Live);
    }

    [Fact(DisplayName = "ListBoardCards → excludes archived cards even if the server returns them")]
    public async Task ListBoardCards_excludes_archived()
    {
        var provider = Provider(
            listCards: (_, _, _) => Task.FromResult<IReadOnlyList<KaitenCard>>(
            [
                SampleCard(columnId: 100, id: 1),
                SampleCard(columnId: 100, id: 2, condition: KaitenCardConditions.Archived),
            ]),
            columns: (_, _, _) => Task.FromResult<IReadOnlyList<KaitenColumn>>(
                [new KaitenColumn(100, "In Progress", 10, 0)]));

        var cards = await provider.ListBoardCardsAsync(Descriptor, "10", CancellationToken.None);

        cards.Should().ContainSingle().Which.CardId.Should().Be("1");
    }

    [Theory(DisplayName = "ListBoardCards → classifies upstream failures per ADR-0053")]
    [InlineData(HttpStatusCode.Unauthorized, TaskTrackerConnectionHealth.Auth)]
    [InlineData(HttpStatusCode.Forbidden, TaskTrackerConnectionHealth.Auth)]
    [InlineData(HttpStatusCode.PaymentRequired, TaskTrackerConnectionHealth.Blocked)]
    [InlineData(HttpStatusCode.BadGateway, TaskTrackerConnectionHealth.Offline)]
    public async Task ListBoardCards_classifies_failures(HttpStatusCode status, TaskTrackerConnectionHealth expected)
    {
        var provider = Provider(
            listCards: (_, _, _) => throw new KaitenApiException(status, body: null));

        var act = () => provider.ListBoardCardsAsync(Descriptor, "10", CancellationToken.None);

        (await act.Should().ThrowAsync<TaskTrackerConnectionException>())
            .Which.Health.Should().Be(expected);
    }

    [Fact(DisplayName = "ListBoardCards → transport failure is Offline")]
    public async Task ListBoardCards_transport_is_offline()
    {
        var provider = Provider(
            listCards: (_, _, _) => throw new HttpRequestException("no route"));

        var act = () => provider.ListBoardCardsAsync(Descriptor, "10", CancellationToken.None);

        (await act.Should().ThrowAsync<TaskTrackerConnectionException>())
            .Which.Health.Should().Be(TaskTrackerConnectionHealth.Offline);
    }
}
