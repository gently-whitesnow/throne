using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Application.Intents;

public sealed record SetIntentStatusCommand(
    string IntentId,
    string Status,
    string? Reason,
    IntentTrainingAuthor ChangedBy,
    string Source);

public sealed class SetIntentStatusHandler(
    IIntentRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Intent> HandleAsync(SetIntentStatusCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        Validate(command);

        var now = clock.GetUtcNow();
        var id = new IntentId(command.IntentId);
        var normalizedReason = string.IsNullOrWhiteSpace(command.Reason) ? null : command.Reason.Trim();
        var appendText = BuildRejectAppendText(command.Status, normalizedReason);

        var outcome = await unitOfWork.ExecuteAsync(
            inner => repository.SetStatusAsync(
                id,
                command.Status,
                appendText,
                normalizedReason,
                command.ChangedBy,
                command.Source,
                now,
                inner),
            ct);

        return outcome switch
        {
            SetIntentStatusOutcome.Updated updated => updated.Intent,
            SetIntentStatusOutcome.NotFound => throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{command.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = command.IntentId }),
            SetIntentStatusOutcome.Conflict conflict => throw new ApiException(
                ErrorCodes.IntentVersionConflict,
                "Intent state changed concurrently; reload and retry.",
                new Dictionary<string, object?>
                {
                    ["intent_id"] = command.IntentId,
                    ["current_version"] = conflict.CurrentVersion,
                    ["current_status"] = conflict.CurrentStatus,
                }),
            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
    }

    private static void Validate(SetIntentStatusCommand command)
    {
        if (!IntentStatusNames.IsKnown(command.Status))
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "Unknown intent status.",
                new Dictionary<string, object?>
                {
                    ["status"] = command.Status,
                    ["allowed_statuses"] = IntentStatusNames.All.ToArray(),
                });
        }

        var hasReason = !string.IsNullOrWhiteSpace(command.Reason);
        if (command.Status == IntentStatusNames.Reject && !hasReason)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "Reject status requires a reason.",
                new Dictionary<string, object?>
                {
                    ["status"] = command.Status,
                    ["field"] = "reason",
                });
        }
    }

    private static string? BuildRejectAppendText(string status, string? reason)
    {
        if (status != IntentStatusNames.Reject || string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        return $"\n\nПричина отклонения:\n{reason}\n";
    }
}
