using Throne.Infrastructure.TaskTrackers.Kaiten.Http;
using Throne.Infrastructure.TaskTrackers.Kaiten.Models;

namespace Throne.Infrastructure.TaskTrackers.Kaiten;

internal sealed class KaitenCommentsApi(KaitenHttpExecutor http) : IKaitenCommentsApi
{
    public Task<IReadOnlyList<KaitenComment>> ListCommentsAsync(KaitenConnection connection, long cardId, CancellationToken ct) =>
        http.GetAsync<IReadOnlyList<KaitenComment>>(connection, $"/cards/{cardId}/comments", ct);

    public Task<KaitenComment> AddCommentAsync(
        KaitenConnection connection, long cardId, KaitenCreateCommentRequest request, CancellationToken ct) =>
        http.PostAsync<KaitenComment>(connection, $"/cards/{cardId}/comments", request, ct);
}
