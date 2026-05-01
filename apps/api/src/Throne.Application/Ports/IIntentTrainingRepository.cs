using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Application.Ports;

public interface IIntentTrainingRepository
{
    Task<AppendTrainingOutcome> AddQaAsync(
        IntentId id,
        int expectedVersion,
        IntentQa qa,
        DateTimeOffset now,
        CancellationToken ct);

    Task<AppendTrainingOutcome> AddReviewAsync(
        IntentId id,
        int expectedVersion,
        IntentReview review,
        DateTimeOffset now,
        CancellationToken ct);
}
