using Throne.Infrastructure.TaskTrackers.Kaiten.Http;
using Throne.Infrastructure.TaskTrackers.Kaiten.Models;

namespace Throne.Infrastructure.TaskTrackers.Kaiten;

internal sealed class KaitenTopologyApi(KaitenHttpExecutor http) : IKaitenTopologyApi
{
    public Task<IReadOnlyList<KaitenSpace>> ListSpacesAsync(KaitenConnection connection, CancellationToken ct) =>
        http.GetAsync<IReadOnlyList<KaitenSpace>>(connection, "/spaces", ct);

    public Task<KaitenSpace> GetSpaceAsync(KaitenConnection connection, long spaceId, CancellationToken ct) =>
        http.GetAsync<KaitenSpace>(connection, $"/spaces/{spaceId}", ct);

    public Task<IReadOnlyList<KaitenBoard>> ListBoardsAsync(KaitenConnection connection, long spaceId, CancellationToken ct) =>
        http.GetAsync<IReadOnlyList<KaitenBoard>>(connection, $"/spaces/{spaceId}/boards", ct);

    public Task<KaitenBoard> GetBoardAsync(KaitenConnection connection, long boardId, CancellationToken ct) =>
        http.GetAsync<KaitenBoard>(connection, $"/boards/{boardId}", ct);

    public Task<IReadOnlyList<KaitenColumn>> ListColumnsAsync(KaitenConnection connection, long boardId, CancellationToken ct) =>
        http.GetAsync<IReadOnlyList<KaitenColumn>>(connection, $"/boards/{boardId}/columns", ct);

    public Task<IReadOnlyList<KaitenLane>> ListLanesAsync(KaitenConnection connection, long boardId, CancellationToken ct) =>
        http.GetAsync<IReadOnlyList<KaitenLane>>(connection, $"/boards/{boardId}/lanes", ct);
}
