using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Intents;

public sealed record GetIntentQuery(string IntentId);

public sealed class GetIntentHandler(IIntentRepository repository)
{
    public async Task<Intent> HandleAsync(GetIntentQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        return await repository.GetByIdAsync(new IntentId(query.IntentId), ct).ConfigureAwait(false)
            ?? throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{query.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = query.IntentId });
    }
}
