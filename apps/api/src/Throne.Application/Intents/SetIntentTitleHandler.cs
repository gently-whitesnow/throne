using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Intents;

public sealed record SetIntentTitleCommand(
    string IntentId,
    int ExpectedVersion,
    string? Title);

/// <summary>
/// Sets the optional intent title. <c>null</c>/blank clears it. The title is free intent metadata:
/// attached task-tracker cards are read-only context (ADR-0052) and impose no invariant on it.
/// </summary>
public sealed class SetIntentTitleHandler(
    IIntentRepository intentRepository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Intent> HandleAsync(SetIntentTitleCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var id = new IntentId(command.IntentId);
        var now = clock.GetUtcNow();

        var outcome = await unitOfWork.ExecuteAsync(
            inner => intentRepository.SetTitleAsync(id, command.ExpectedVersion, command.Title, now, inner),
            ct);

        return ProjectOutcome(outcome, command);
    }

    private static Intent ProjectOutcome(SetIntentTitleOutcome outcome, SetIntentTitleCommand command) =>
        outcome switch
        {
            SetIntentTitleOutcome.Updated updated => updated.Intent,
            SetIntentTitleOutcome.NotFound => throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{command.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = command.IntentId }),
            SetIntentTitleOutcome.VersionConflict conflict => throw new ApiException(
                ErrorCodes.IntentVersionConflict,
                $"Intent version conflict (current_version={conflict.CurrentVersion}).",
                new Dictionary<string, object?>
                {
                    ["intent_id"] = command.IntentId,
                    ["expected_version"] = command.ExpectedVersion,
                    ["current_version"] = conflict.CurrentVersion,
                }),
            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
}
