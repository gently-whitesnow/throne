using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.TextVersions;

namespace Throne.Application.Intents;

public sealed record ReplaceIntentTextCommand(
    string IntentId,
    int ExpectedVersion,
    string OldText,
    string NewText,
    TextVersionAuthor Author);

public sealed class ReplaceIntentTextHandler(
    IIntentRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Intent> HandleAsync(ReplaceIntentTextCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.OldText);
        ArgumentNullException.ThrowIfNull(command.NewText);
        if (command.OldText.Length == 0)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "old_text must not be empty.",
                new Dictionary<string, object?> { ["field"] = "old_text" });
        }

        var id = new IntentId(command.IntentId);
        var now = clock.GetUtcNow();

        var outcome = await unitOfWork.ExecuteAsync(
            inner => repository.ReplaceTextAsync(id, command.ExpectedVersion, command.OldText, command.NewText, command.Author, now, inner),
            ct);

        return outcome switch
        {
            ReplaceIntentTextOutcome.Replaced replaced => replaced.Intent,

            ReplaceIntentTextOutcome.NotFound => throw new ApiException(
                ErrorCodes.IntentNotFound,
                $"Intent '{command.IntentId}' not found.",
                new Dictionary<string, object?> { ["intent_id"] = command.IntentId }),

            ReplaceIntentTextOutcome.VersionConflict conflict => throw new ApiException(
                ErrorCodes.IntentVersionConflict,
                "Intent.current_version does not match expected_version.",
                new Dictionary<string, object?>
                {
                    ["intent_id"] = command.IntentId,
                    ["expected_version"] = command.ExpectedVersion,
                    ["current_version"] = conflict.CurrentVersion,
                }),

            ReplaceIntentTextOutcome.MatchNotFound notFound => throw new ApiException(
                ErrorCodes.IntentTextMatchNotFound,
                "old_text was not found in Intent.text.",
                new Dictionary<string, object?>
                {
                    ["intent_id"] = command.IntentId,
                    ["query_preview"] = notFound.QueryPreview,
                    ["hint"] = "use search_intent_text or extend old_text with neighbouring context to make it unique",
                }),

            ReplaceIntentTextOutcome.MatchAmbiguous ambiguous => throw new ApiException(
                ErrorCodes.IntentTextMatchAmbiguous,
                "old_text matches more than one location in Intent.text.",
                new Dictionary<string, object?>
                {
                    ["intent_id"] = command.IntentId,
                    ["matches_count"] = ambiguous.MatchesCount,
                    ["match_lines"] = ambiguous.MatchLines,
                    ["hint"] = "extend old_text so it becomes unique",
                }),

            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
    }
}
