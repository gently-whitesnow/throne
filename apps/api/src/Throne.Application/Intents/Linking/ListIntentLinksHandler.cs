using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Intents.Linking;

public sealed class ListIntentLinksHandler(IIntentLinkRepository repository)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public Task<IntentLinksPage> HandleAsync(ListIntentLinksQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.IntentId);

        var limit = query.Limit <= 0 ? DefaultLimit : Math.Min(query.Limit, MaxLimit);
        return repository.ListPagedAsync(
            new IntentId(query.IntentId),
            query.Direction,
            query.Type,
            limit,
            query.Cursor,
            ct);
    }
}
