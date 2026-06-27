using Throne.Infrastructure.TaskTrackers.Kaiten.Http;
using Throne.Infrastructure.TaskTrackers.Kaiten.Models;

namespace Throne.Infrastructure.TaskTrackers.Kaiten;

internal sealed class KaitenTagsApi(KaitenHttpExecutor http) : IKaitenTagsApi
{
    public Task<IReadOnlyList<KaitenTag>> ListCompanyTagsAsync(KaitenConnection connection, CancellationToken ct) =>
        http.GetAsync<IReadOnlyList<KaitenTag>>(connection, "/tags", ct);

    public Task<IReadOnlyList<KaitenCardTag>> ListCardTagsAsync(KaitenConnection connection, long cardId, CancellationToken ct) =>
        http.GetAsync<IReadOnlyList<KaitenCardTag>>(connection, $"/cards/{cardId}/tags", ct);

    public Task<KaitenTag> AddCardTagAsync(
        KaitenConnection connection, long cardId, KaitenAddCardTagRequest request, CancellationToken ct) =>
        http.PostAsync<KaitenTag>(connection, $"/cards/{cardId}/tags", request, ct);

    public Task RemoveCardTagAsync(KaitenConnection connection, long cardId, long tagId, CancellationToken ct) =>
        http.DeleteAsync(connection, $"/cards/{cardId}/tags/{tagId}", ct);
}
