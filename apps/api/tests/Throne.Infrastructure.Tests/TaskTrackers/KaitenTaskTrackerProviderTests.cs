using System.Net;
using FluentAssertions;
using Throne.Application.Errors;
using Throne.Application.TaskTrackers;
using Throne.Infrastructure.TaskTrackers;
using Throne.Infrastructure.TaskTrackers.Kaiten;
using Throne.Infrastructure.TaskTrackers.Kaiten.Models;

namespace Throne.Infrastructure.Tests.TaskTrackers;

public sealed class KaitenTaskTrackerProviderTests
{
    private static readonly TaskTrackerConnectionDescriptor Descriptor = new("https://acme.kaiten.ru", "tok");

    [Fact(DisplayName = "Probe → Connected when the topology read succeeds")]
    public async Task Probe_connected()
    {
        var provider = Provider(spaces: (_, _) => Task.FromResult<IReadOnlyList<KaitenSpace>>([]));

        var result = await provider.ProbeAsync(Descriptor, CancellationToken.None);

        result.Health.Should().Be(TaskTrackerConnectionHealth.Connected);
    }

    [Theory(DisplayName = "Probe → Invalid on 401/403 (token rejected)")]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Probe_invalid_on_auth_failure(HttpStatusCode status)
    {
        var provider = Provider(spaces: (_, _) => throw new KaitenApiException(status, body: null));

        var result = await provider.ProbeAsync(Descriptor, CancellationToken.None);

        result.Health.Should().Be(TaskTrackerConnectionHealth.Invalid);
    }

    [Fact(DisplayName = "Probe → Unreachable on a 5xx or a transport failure")]
    public async Task Probe_unreachable()
    {
        var server = Provider(spaces: (_, _) => throw new KaitenApiException(HttpStatusCode.BadGateway, body: null));
        var transport = Provider(spaces: (_, _) => throw new HttpRequestException("no route"));

        (await server.ProbeAsync(Descriptor, CancellationToken.None)).Health
            .Should().Be(TaskTrackerConnectionHealth.Unreachable);
        (await transport.ProbeAsync(Descriptor, CancellationToken.None)).Health
            .Should().Be(TaskTrackerConnectionHealth.Unreachable);
    }

    [Fact(DisplayName = "ListBoards maps spaces and boards to opaque string ids")]
    public async Task ListBoards_maps_topology()
    {
        var provider = Provider(
            spaces: (_, _) => Task.FromResult<IReadOnlyList<KaitenSpace>>(
                [new KaitenSpace(1, "Space One", null)]),
            boards: (_, spaceId, _) => Task.FromResult<IReadOnlyList<KaitenBoard>>(
                [new KaitenBoard(10, "Board Ten", null, spaceId)]));

        var topology = await provider.ListBoardsAsync(Descriptor, CancellationToken.None);

        topology.Should().ContainSingle();
        topology[0].SpaceId.Should().Be("1");
        topology[0].SpaceTitle.Should().Be("Space One");
        topology[0].Boards.Should().ContainSingle();
        topology[0].Boards[0].BoardId.Should().Be("10");
        topology[0].Boards[0].BoardTitle.Should().Be("Board Ten");
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

    private static KaitenTaskTrackerProvider Provider(
        Func<KaitenConnection, CancellationToken, Task<IReadOnlyList<KaitenSpace>>> spaces,
        Func<KaitenConnection, long, CancellationToken, Task<IReadOnlyList<KaitenBoard>>>? boards = null) =>
        new(new StubKaitenClient(new StubTopologyApi(spaces, boards)));

    private sealed class StubKaitenClient(IKaitenTopologyApi topology) : IKaitenClient
    {
        public IKaitenTopologyApi Topology { get; } = topology;
        public IKaitenCardsApi Cards => throw new NotSupportedException();
        public IKaitenCommentsApi Comments => throw new NotSupportedException();
        public IKaitenTagsApi Tags => throw new NotSupportedException();
        public IKaitenCardChildrenApi CardChildren => throw new NotSupportedException();
    }

    private sealed class StubTopologyApi(
        Func<KaitenConnection, CancellationToken, Task<IReadOnlyList<KaitenSpace>>> spaces,
        Func<KaitenConnection, long, CancellationToken, Task<IReadOnlyList<KaitenBoard>>>? boards) : IKaitenTopologyApi
    {
        public Task<IReadOnlyList<KaitenSpace>> ListSpacesAsync(KaitenConnection connection, CancellationToken ct) =>
            spaces(connection, ct);

        public Task<IReadOnlyList<KaitenBoard>> ListBoardsAsync(KaitenConnection connection, long spaceId, CancellationToken ct) =>
            (boards ?? throw new NotSupportedException())(connection, spaceId, ct);

        public Task<KaitenSpace> GetSpaceAsync(KaitenConnection connection, long spaceId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<KaitenBoard> GetBoardAsync(KaitenConnection connection, long boardId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<KaitenColumn>> ListColumnsAsync(KaitenConnection connection, long boardId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<KaitenLane>> ListLanesAsync(KaitenConnection connection, long boardId, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
