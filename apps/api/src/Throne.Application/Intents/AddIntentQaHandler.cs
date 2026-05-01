using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Application.Intents;

public sealed record AddIntentQaCommand(
    string IntentId,
    int ExpectedVersion,
    string Question,
    string Answer);

public sealed class AddIntentQaHandler(
    IIntentTrainingRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Ack> HandleAsync(AddIntentQaCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Question);
        ArgumentNullException.ThrowIfNull(command.Answer);
        if (command.Question.Length == 0)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "question must not be empty.",
                new Dictionary<string, object?> { ["field"] = "question" });
        }

        if (command.Answer.Length == 0)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "answer must not be empty.",
                new Dictionary<string, object?> { ["field"] = "answer" });
        }

        var id = new IntentId(command.IntentId);
        var now = clock.GetUtcNow();
        var qa = IntentQa.Create(
            id: Guid.NewGuid().ToString("N"),
            intentId: id,
            intentVersionAtWrite: command.ExpectedVersion,
            question: command.Question,
            answer: command.Answer,
            now: now,
            createdBy: IntentTrainingAuthor.Agent);

        var outcome = await unitOfWork.ExecuteAsync(
            inner => repository.AddQaAsync(id, command.ExpectedVersion, qa, now, inner),
            ct).ConfigureAwait(false);

        return outcome switch
        {
            AppendTrainingOutcome.Appended appended => new Ack(command.IntentId, appended.CurrentVersion),

            AppendTrainingOutcome.NotFound => throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{command.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = command.IntentId }),

            AppendTrainingOutcome.VersionConflict conflict => throw new ApiException(
                ErrorCodes.IntentVersionConflict,
                "Intent.current_version does not match expected_version.",
                new Dictionary<string, object?>
                {
                    ["intent_id"] = command.IntentId,
                    ["expected_version"] = command.ExpectedVersion,
                    ["current_version"] = conflict.CurrentVersion,
                }),

            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
    }
}
