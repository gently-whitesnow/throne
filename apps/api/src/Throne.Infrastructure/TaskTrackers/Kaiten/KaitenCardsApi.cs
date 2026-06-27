using Throne.Infrastructure.TaskTrackers.Kaiten.Http;
using Throne.Infrastructure.TaskTrackers.Kaiten.Models;

namespace Throne.Infrastructure.TaskTrackers.Kaiten;

internal sealed class KaitenCardsApi(KaitenHttpExecutor http) : IKaitenCardsApi
{
    private const int DefaultPageSize = 100;

    public Task<IReadOnlyList<KaitenCard>> ListCardsAsync(KaitenConnection connection, KaitenCardQuery query, CancellationToken ct) =>
        http.GetAsync<IReadOnlyList<KaitenCard>>(connection, "/cards" + query.ToQueryString(), ct);

    public async Task<IReadOnlyList<KaitenCard>> ListAllCardsAsync(
        KaitenConnection connection, KaitenCardQuery query, CancellationToken ct)
    {
        var pageSize = query.Limit ?? DefaultPageSize;
        var offset = query.Offset ?? 0;
        var all = new List<KaitenCard>();
        while (true)
        {
            var page = await ListCardsAsync(connection, query with { Limit = pageSize, Offset = offset }, ct);
            all.AddRange(page);
            if (page.Count < pageSize)
            {
                return all;
            }

            offset += pageSize;
        }
    }

    public Task<KaitenCard> GetCardAsync(KaitenConnection connection, long cardId, CancellationToken ct) =>
        http.GetAsync<KaitenCard>(connection, $"/cards/{cardId}", ct);

    public Task<KaitenCard> CreateCardAsync(KaitenConnection connection, KaitenCreateCardRequest request, CancellationToken ct) =>
        http.PostAsync<KaitenCard>(connection, "/cards", request, ct);

    public Task<KaitenCard> UpdateCardAsync(KaitenConnection connection, long cardId, KaitenUpdateCardRequest request, CancellationToken ct) =>
        http.PatchAsync<KaitenCard>(connection, $"/cards/{cardId}", request, ct);
}
