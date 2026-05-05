using Throne.Application.Auth;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Application.Intents;

public sealed record AddIntentReviewCommand(
    string IntentId,
    int ExpectedVersion,
    string Note,
    string Reason);

public sealed class AddIntentReviewHandler(
    IIntentTrainingRepository repository,
    IUnitOfWork unitOfWork,
    ICurrentUserAccessor currentUser,
    TimeProvider clock)
{
    public async Task<Ack> HandleAsync(AddIntentReviewCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Note);
        ArgumentNullException.ThrowIfNull(command.Reason);
        if (command.Note.Length == 0)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "note must not be empty.",
                new Dictionary<string, object?> { ["field"] = "note" });
        }

        if (command.Reason.Length == 0)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "reason must not be empty.",
                new Dictionary<string, object?> { ["field"] = "reason" });
        }

        var id = new IntentId(command.IntentId);
        var now = clock.GetUtcNow();
        var review = IntentReview.Create(
            id: Guid.NewGuid().ToString("N"),
            ownerUserId: currentUser.UserId,
            intentId: id,
            intentVersionAtWrite: command.ExpectedVersion,
            note: command.Note,
            reason: command.Reason,
            now: now,
            createdBy: IntentTrainingAuthor.Agent);

        var outcome = await unitOfWork.ExecuteAsync(
            inner => repository.AddReviewAsync(id, command.ExpectedVersion, review, now, inner),
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
