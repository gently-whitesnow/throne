using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Intents;

public sealed record ListIntentsQuery(IReadOnlyList<string>? Statuses = null);

public sealed class ListIntentsHandler(IIntentRepository repository)
{
    public async Task<IReadOnlyList<Intent>> HandleAsync(ListIntentsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await repository.ListAsync(query.Statuses, ct).ConfigureAwait(false);
    }
}
