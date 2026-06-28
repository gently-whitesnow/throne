using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;

namespace Throne.Application.Intents;

public sealed record SetIntentTitleCommand(
    string IntentId,
    int ExpectedVersion,
    string? Title);

/// <summary>
/// Sets the optional intent title. Enforces the write-path rule «a card-linked intent must keep a
/// non-empty title» here (not in the aggregate, which knows nothing about mirror links): every tracker
/// requires a card title, so clearing it would leave the mirror push with nothing to send.
/// </summary>
public sealed class SetIntentTitleHandler(
    IIntentRepository intentRepository,
    ITaskTrackerCardLinkStore linkStore,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Intent> HandleAsync(SetIntentTitleCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var id = new IntentId(command.IntentId);
        var now = clock.GetUtcNow();

        if (string.IsNullOrWhiteSpace(command.Title))
        {
            var link = await linkStore.GetByIntentAsync(command.IntentId, ct);
            if (link is { IsStub: false })
            {
                throw new ApiException(
                    ErrorCodes.ValidationFailed,
                    "A card-linked intent must keep a non-empty title.",
                    new Dictionary<string, object?>
                    {
                        ["intent_id"] = command.IntentId,
                        ["field"] = "title",
                    });
            }
        }

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
