using Throne.Application.TaskTrackers;
using Throne.Infrastructure.TaskTrackers;
using Throne.Infrastructure.TaskTrackers.Kaiten;
using Throne.Infrastructure.TaskTrackers.Kaiten.Models;

namespace Throne.Infrastructure.Tests.TaskTrackers;

/// <summary>
/// Shared stub harness for the Kaiten provider tests: a <see cref="KaitenTaskTrackerProvider"/> over
/// hand-written topology/cards stubs whose behaviour is injected per test. Split out of the test
/// classes so probe/board and card suites stay under the public-member budget without duplicating it.
/// </summary>
internal static class KaitenProviderTestHarness
{
    public static readonly TaskTrackerConnectionDescriptor Descriptor = new("https://acme.kaiten.ru", "tok");

    public static KaitenTaskTrackerProvider Provider(
        Func<KaitenConnection, CancellationToken, Task<IReadOnlyList<KaitenSpace>>>? spaces = null,
        Func<KaitenConnection, long, CancellationToken, Task<IReadOnlyList<KaitenBoard>>>? boards = null,
        Func<KaitenConnection, long, CancellationToken, Task<KaitenCard>>? card = null,
        Func<KaitenConnection, KaitenCardQuery, CancellationToken, Task<IReadOnlyList<KaitenCard>>>? listCards = null,
        Func<KaitenConnection, long, CancellationToken, Task<IReadOnlyList<KaitenColumn>>>? columns = null) =>
        new(new StubKaitenClient(
            new StubTopologyApi(
                spaces ?? ((_, _) => Task.FromResult<IReadOnlyList<KaitenSpace>>([])),
                boards,
                columns),
            new StubCardsApi(card, listCards)));

    public static KaitenCard SampleCard(long columnId, long id = 42, int condition = KaitenCardConditions.Live) =>
        new(
            Id: id,
            Title: "Card",
            Description: "body",
            BoardId: 10,
            ColumnId: columnId,
            LaneId: null,
            TagIds: null,
            Condition: condition,
            Created: null,
            Updated: null,
            ColumnChangedAt: null,
            TypeId: null);

    private sealed class StubKaitenClient(IKaitenTopologyApi topology, IKaitenCardsApi cards) : IKaitenClient
    {
        public IKaitenTopologyApi Topology { get; } = topology;
        public IKaitenCardsApi Cards { get; } = cards;
        public IKaitenCommentsApi Comments => throw new NotSupportedException();
        public IKaitenTagsApi Tags => throw new NotSupportedException();
        public IKaitenCardChildrenApi CardChildren => throw new NotSupportedException();
    }

    private sealed class StubCardsApi(
        Func<KaitenConnection, long, CancellationToken, Task<KaitenCard>>? card,
        Func<KaitenConnection, KaitenCardQuery, CancellationToken, Task<IReadOnlyList<KaitenCard>>>? listCards) : IKaitenCardsApi
    {
        public Task<KaitenCard> GetCardAsync(KaitenConnection connection, long cardId, CancellationToken ct) =>
            (card ?? throw new NotSupportedException())(connection, cardId, ct);

        public Task<IReadOnlyList<KaitenCard>> ListCardsAsync(KaitenConnection connection, KaitenCardQuery query, CancellationToken ct) =>
            (listCards ?? throw new NotSupportedException())(connection, query, ct);

        public Task<IReadOnlyList<KaitenCard>> ListAllCardsAsync(KaitenConnection connection, KaitenCardQuery query, CancellationToken ct) =>
            (listCards ?? throw new NotSupportedException())(connection, query, ct);

        public Task<KaitenCard> CreateCardAsync(KaitenConnection connection, KaitenCreateCardRequest request, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<KaitenCard> UpdateCardAsync(KaitenConnection connection, long cardId, KaitenUpdateCardRequest request, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class StubTopologyApi(
        Func<KaitenConnection, CancellationToken, Task<IReadOnlyList<KaitenSpace>>> spaces,
        Func<KaitenConnection, long, CancellationToken, Task<IReadOnlyList<KaitenBoard>>>? boards,
        Func<KaitenConnection, long, CancellationToken, Task<IReadOnlyList<KaitenColumn>>>? columns) : IKaitenTopologyApi
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
            (columns ?? ((_, _, _) => Task.FromResult<IReadOnlyList<KaitenColumn>>([])))(connection, boardId, ct);

        public Task<IReadOnlyList<KaitenLane>> ListLanesAsync(KaitenConnection connection, long boardId, CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
