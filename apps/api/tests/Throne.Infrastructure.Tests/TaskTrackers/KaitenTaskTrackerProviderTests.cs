using System.Net;
using FluentAssertions;
using Throne.Application.Errors;
using Throne.Application.TaskTrackers;
using Throne.Infrastructure.TaskTrackers.Kaiten;
using Throne.Infrastructure.TaskTrackers.Kaiten.Models;
using static Throne.Infrastructure.Tests.TaskTrackers.KaitenProviderTestHarness;

namespace Throne.Infrastructure.Tests.TaskTrackers;

public sealed class KaitenTaskTrackerProviderTests
{
    [Fact(DisplayName = "Probe → Connected when the topology read succeeds")]
    public async Task Probe_connected()
    {
        var provider = Provider(spaces: (_, _) => Task.FromResult<IReadOnlyList<KaitenSpace>>([]));

        var result = await provider.ProbeAsync(Descriptor, CancellationToken.None);

        result.Health.Should().Be(TaskTrackerConnectionHealth.Connected);
    }

    [Theory(DisplayName = "Probe → Auth on 401/403 (token rejected)")]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Probe_auth_on_auth_failure(HttpStatusCode status)
    {
        var provider = Provider(spaces: (_, _) => throw new KaitenApiException(status, body: null));

        var result = await provider.ProbeAsync(Descriptor, CancellationToken.None);

        result.Health.Should().Be(TaskTrackerConnectionHealth.Auth);
    }

    [Fact(DisplayName = "Probe → Blocked on a 402 (tariff wall)")]
    public async Task Probe_blocked_on_payment_required()
    {
        var provider = Provider(
            spaces: (_, _) => throw new KaitenApiException(HttpStatusCode.PaymentRequired, body: null));

        var result = await provider.ProbeAsync(Descriptor, CancellationToken.None);

        result.Health.Should().Be(TaskTrackerConnectionHealth.Blocked);
    }

    [Fact(DisplayName = "Probe → Offline on a 5xx or a transport failure")]
    public async Task Probe_offline()
    {
        var server = Provider(spaces: (_, _) => throw new KaitenApiException(HttpStatusCode.BadGateway, body: null));
        var transport = Provider(spaces: (_, _) => throw new HttpRequestException("no route"));

        (await server.ProbeAsync(Descriptor, CancellationToken.None)).Health
            .Should().Be(TaskTrackerConnectionHealth.Offline);
        (await transport.ProbeAsync(Descriptor, CancellationToken.None)).Health
            .Should().Be(TaskTrackerConnectionHealth.Offline);
    }

    [Fact(DisplayName = "Probe → Offline on a timeout (OperationCanceled while ct is not cancelled)")]
    public async Task Probe_offline_on_timeout()
    {
        var provider = Provider(spaces: (_, _) => throw new OperationCanceledException("timed out"));

        var result = await provider.ProbeAsync(Descriptor, CancellationToken.None);

        result.Health.Should().Be(TaskTrackerConnectionHealth.Offline);
    }

    [Fact(DisplayName = "ListBoards reads boards nested in spaces (one call) and maps to opaque string ids")]
    public async Task ListBoards_maps_topology()
    {
        var provider = Provider(
            spaces: (_, _) => Task.FromResult<IReadOnlyList<KaitenSpace>>(
                [new KaitenSpace(1, "Space One", null, Boards: [new KaitenBoard(10, "Board Ten", null, 1)])]));

        var topology = await provider.ListBoardsAsync(Descriptor, CancellationToken.None);

        topology.Should().ContainSingle();
        topology[0].SpaceId.Should().Be("1");
        topology[0].SpaceTitle.Should().Be("Space One");
        topology[0].Boards.Should().ContainSingle();
        topology[0].Boards[0].BoardId.Should().Be("10");
        topology[0].Boards[0].BoardTitle.Should().Be("Board Ten");
    }

    [Fact(DisplayName = "ListBoards skips archived spaces")]
    public async Task ListBoards_skips_archived_spaces()
    {
        var provider = Provider(
            spaces: (_, _) => Task.FromResult<IReadOnlyList<KaitenSpace>>(
            [
                new KaitenSpace(1, "Live", null, Boards: [new KaitenBoard(10, "Keep", null, 1)]),
                new KaitenSpace(2, "Gone", null, Archived: true, Boards: [new KaitenBoard(20, "Drop", null, 2)]),
            ]));

        var topology = await provider.ListBoardsAsync(Descriptor, CancellationToken.None);

        topology.Should().ContainSingle();
        topology[0].SpaceTitle.Should().Be("Live");
    }

    [Fact(DisplayName = "ListBoards translates an upstream failure into an upstream-unavailable ApiException")]
    public async Task ListBoards_upstream_failure()
    {
        var provider = Provider(
            spaces: (_, _) => throw new KaitenApiException(HttpStatusCode.BadGateway, body: null));

        var act = () => provider.ListBoardsAsync(Descriptor, CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>())
            .Which.Code.Should().Be(ErrorCodes.TaskTrackerUpstreamUnavailable);
    }

    [Theory(DisplayName = "ListBoards translates a 401/403 into connection-rejected, not upstream-unavailable")]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ListBoards_token_rejected(HttpStatusCode status)
    {
        var provider = Provider(
            spaces: (_, _) => throw new KaitenApiException(status, body: null));

        var act = () => provider.ListBoardsAsync(Descriptor, CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>())
            .Which.Code.Should().Be(ErrorCodes.TaskTrackerConnectionRejected);
    }

    [Fact(DisplayName = "ListBoards translates a 402 into connection-blocked (tariff wall)")]
    public async Task ListBoards_blocked()
    {
        var provider = Provider(
            spaces: (_, _) => throw new KaitenApiException(HttpStatusCode.PaymentRequired, body: null));

        var act = () => provider.ListBoardsAsync(Descriptor, CancellationToken.None);

        (await act.Should().ThrowAsync<ApiException>())
            .Which.Code.Should().Be(ErrorCodes.TaskTrackerConnectionBlocked);
    }
}
