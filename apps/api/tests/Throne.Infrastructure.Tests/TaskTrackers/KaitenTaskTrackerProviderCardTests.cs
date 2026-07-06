using System.Net;
using FluentAssertions;
using Throne.Application.TaskTrackers;
using Throne.Infrastructure.TaskTrackers.Kaiten;
using Throne.Infrastructure.TaskTrackers.Kaiten.Models;
using static Throne.Infrastructure.Tests.TaskTrackers.KaitenProviderTestHarness;

namespace Throne.Infrastructure.Tests.TaskTrackers;

public sealed class KaitenTaskTrackerProviderCardTests
{
    [Fact(DisplayName = "GetCard → projects the card, resolving its column title from the board columns")]
    public async Task GetCard_projects_card()
    {
        var provider = Provider(
            card: (_, _, _) => Task.FromResult(SampleCard(columnId: 100)),
            columns: (_, _, _) => Task.FromResult<IReadOnlyList<KaitenColumn>>(
                [new KaitenColumn(100, "In Progress", 10, 0)]));

        var card = await provider.GetCardAsync(Descriptor, "42", CancellationToken.None);

        card.Should().NotBeNull();
        card!.CardId.Should().Be("42");
        card.BoardId.Should().Be("10");
        card.ColumnTitle.Should().Be("In Progress");
    }

    [Fact(DisplayName = "GetCard → null when the card is genuinely gone (404)")]
    public async Task GetCard_gone_returns_null()
    {
        var provider = Provider(
            card: (_, _, _) => throw new KaitenApiException(HttpStatusCode.NotFound, body: null));

        var card = await provider.GetCardAsync(Descriptor, "42", CancellationToken.None);

        card.Should().BeNull();
    }

    [Theory(DisplayName = "GetCard → throws connection exception with Auth on 401/403 (403 is NOT gone)")]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task GetCard_auth_throws(HttpStatusCode status)
    {
        var provider = Provider(card: (_, _, _) => throw new KaitenApiException(status, body: null));

        var act = () => provider.GetCardAsync(Descriptor, "42", CancellationToken.None);

        (await act.Should().ThrowAsync<TaskTrackerConnectionException>())
            .Which.Health.Should().Be(TaskTrackerConnectionHealth.Auth);
    }

    [Fact(DisplayName = "GetCard → throws connection exception with Blocked on 402")]
    public async Task GetCard_blocked_throws()
    {
        var provider = Provider(
            card: (_, _, _) => throw new KaitenApiException(HttpStatusCode.PaymentRequired, body: null));

        var act = () => provider.GetCardAsync(Descriptor, "42", CancellationToken.None);

        (await act.Should().ThrowAsync<TaskTrackerConnectionException>())
            .Which.Health.Should().Be(TaskTrackerConnectionHealth.Blocked);
    }

    [Fact(DisplayName = "GetCard → throws connection exception with Offline on 5xx and on transport failure")]
    public async Task GetCard_offline_throws()
    {
        var server = Provider(card: (_, _, _) => throw new KaitenApiException(HttpStatusCode.BadGateway, body: null));
        var transport = Provider(card: (_, _, _) => throw new HttpRequestException("no route"));

        (await FluentActions.Awaiting(() => server.GetCardAsync(Descriptor, "42", CancellationToken.None))
            .Should().ThrowAsync<TaskTrackerConnectionException>())
            .Which.Health.Should().Be(TaskTrackerConnectionHealth.Offline);
        (await FluentActions.Awaiting(() => transport.GetCardAsync(Descriptor, "42", CancellationToken.None))
            .Should().ThrowAsync<TaskTrackerConnectionException>())
            .Which.Health.Should().Be(TaskTrackerConnectionHealth.Offline);
    }
}
