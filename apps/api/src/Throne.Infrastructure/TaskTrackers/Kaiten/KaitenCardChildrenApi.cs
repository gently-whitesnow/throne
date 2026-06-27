using Throne.Infrastructure.TaskTrackers.Kaiten.Http;
using Throne.Infrastructure.TaskTrackers.Kaiten.Models;

namespace Throne.Infrastructure.TaskTrackers.Kaiten;

internal sealed class KaitenCardChildrenApi(KaitenHttpExecutor http) : IKaitenCardChildrenApi
{
    public Task<IReadOnlyList<KaitenCard>> ListChildrenAsync(KaitenConnection connection, long cardId, CancellationToken ct) =>
        http.GetAsync<IReadOnlyList<KaitenCard>>(connection, $"/cards/{cardId}/children", ct);

    public Task<KaitenCard> AddChildAsync(
        KaitenConnection connection, long cardId, KaitenAddCardChildRequest request, CancellationToken ct) =>
        http.PostAsync<KaitenCard>(connection, $"/cards/{cardId}/children", request, ct);

    public Task RemoveChildAsync(KaitenConnection connection, long cardId, long childCardId, CancellationToken ct) =>
        http.DeleteAsync(connection, $"/cards/{cardId}/children/{childCardId}", ct);
}
