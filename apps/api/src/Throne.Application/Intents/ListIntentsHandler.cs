using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Intents;

public sealed record ListIntentsQuery();

public sealed class ListIntentsHandler(IIntentRepository repository)
{
    public async Task<IReadOnlyList<Intent>> HandleAsync(ListIntentsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await repository.ListAsync(ct).ConfigureAwait(false);
    }
}
