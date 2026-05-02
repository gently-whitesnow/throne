using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Application.Intents;

public sealed record ListIntentQaQuery(string IntentId);

public sealed class ListIntentQaHandler(
    IIntentRepository intents,
    IIntentTrainingRepository training)
{
    public async Task<IReadOnlyList<IntentQa>> HandleAsync(ListIntentQaQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var id = new IntentId(query.IntentId);
        if (await intents.GetByIdAsync(id, ct).ConfigureAwait(false) is null)
        {
            throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{query.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = query.IntentId });
        }

        return await training.ListQaByIntentAsync(id, ct).ConfigureAwait(false);
    }
}
