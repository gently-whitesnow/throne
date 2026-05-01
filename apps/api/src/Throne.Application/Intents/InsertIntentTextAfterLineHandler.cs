using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Intents;

public sealed record InsertIntentTextAfterLineCommand(
    string IntentId,
    int ExpectedVersion,
    int AfterLine,
    string InsertText);

public sealed class InsertIntentTextAfterLineHandler(
    IIntentRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Intent> HandleAsync(InsertIntentTextAfterLineCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.InsertText);
        if (command.AfterLine < 0)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "after_line must be >= 0.",
                new Dictionary<string, object?> { ["field"] = "after_line" });
        }

        var id = new IntentId(command.IntentId);
        var now = clock.GetUtcNow();

        var outcome = await unitOfWork.ExecuteAsync(
            inner => repository.InsertTextAfterLineAsync(id, command.ExpectedVersion, command.AfterLine, command.InsertText, now, inner),
            ct).ConfigureAwait(false);

        return outcome switch
        {
            InsertIntentTextAfterLineOutcome.Inserted inserted => inserted.Intent,

            InsertIntentTextAfterLineOutcome.NotFound => throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{command.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = command.IntentId }),

            InsertIntentTextAfterLineOutcome.VersionConflict conflict => throw new ApiException(
                ErrorCodes.IntentVersionConflict,
                "Intent.current_version does not match expected_version.",
                new Dictionary<string, object?>
                {
                    ["intent_id"] = command.IntentId,
                    ["expected_version"] = command.ExpectedVersion,
                    ["current_version"] = conflict.CurrentVersion,
                }),

            InsertIntentTextAfterLineOutcome.LineOutOfRange outOfRange => throw new ApiException(
                ErrorCodes.IntentTextLineOutOfRange,
                "after_line is out of range.",
                new Dictionary<string, object?>
                {
                    ["intent_id"] = command.IntentId,
                    ["total_lines"] = outOfRange.TotalLines,
                    ["requested_after_line"] = outOfRange.RequestedAfterLine,
                }),

            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
    }
}
