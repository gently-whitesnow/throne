using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Application.Intents;

public sealed record ListIntentReviewsQuery(string IntentId);

public sealed class ListIntentReviewsHandler(
    IIntentRepository intents,
    IIntentTrainingRepository training)
{
    public async Task<IReadOnlyList<IntentReview>> HandleAsync(ListIntentReviewsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var id = new IntentId(query.IntentId);
        if (await intents.GetByIdAsync(id, ct) is null)
        {
            throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{query.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = query.IntentId });
        }

        return await training.ListReviewsByIntentAsync(id, ct);
    }
}
