using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.PromptParts;

namespace Throne.Application.PromptParts;

public sealed record ReplacePromptPartTextCommand(
    string PromptPartId,
    int ExpectedVersion,
    string OldText,
    string NewText);

public sealed class ReplacePromptPartTextHandler(
    IPromptPartRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<PromptPart> HandleAsync(ReplacePromptPartTextCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.OldText);
        ArgumentNullException.ThrowIfNull(command.NewText);

        var id = new PromptPartId(command.PromptPartId);
        var now = clock.GetUtcNow();

        ReplacePromptPartTextOutcome outcome;
        try
        {
            outcome = await unitOfWork.ExecuteAsync(
                inner => repository.ReplaceTextAsync(id, command.ExpectedVersion, command.OldText, command.NewText, now, inner),
                ct);
        }
        catch (ArgumentException ex) when (ex.ParamName == "oldText")
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                ex.Message,
                new Dictionary<string, object?>
                {
                    ["field"] = "old_text",
                    ["prompt_part_id"] = command.PromptPartId,
                });
        }

        return outcome switch
        {
            ReplacePromptPartTextOutcome.Replaced replaced => replaced.Part,

            ReplacePromptPartTextOutcome.NotFound => throw PromptPartErrors.NotFound(command.PromptPartId),

            ReplacePromptPartTextOutcome.VersionConflict conflict => throw new ApiException(
                ErrorCodes.PromptPartVersionConflict,
                "PromptPart.current_version does not match expected_version.",
                new Dictionary<string, object?>
                {
                    ["prompt_part_id"] = command.PromptPartId,
                    ["expected_version"] = command.ExpectedVersion,
                    ["current_version"] = conflict.CurrentVersion,
                }),

            ReplacePromptPartTextOutcome.MatchNotFound notFound => throw new ApiException(
                ErrorCodes.PromptPartTextMatchFailed,
                "old_text was not found in PromptPart.text.",
                new Dictionary<string, object?>
                {
                    ["prompt_part_id"] = command.PromptPartId,
                    ["reason"] = "match_not_found",
                    ["query_preview"] = notFound.QueryPreview,
                    ["hint"] = "extend old_text with neighbouring context to make it unique",
                }),

            ReplacePromptPartTextOutcome.MatchAmbiguous ambiguous => throw new ApiException(
                ErrorCodes.PromptPartTextMatchFailed,
                "old_text matches more than one location in PromptPart.text.",
                new Dictionary<string, object?>
                {
                    ["prompt_part_id"] = command.PromptPartId,
                    ["reason"] = "match_ambiguous",
                    ["matches_count"] = ambiguous.MatchesCount,
                    ["match_lines"] = ambiguous.MatchLines,
                    ["hint"] = "extend old_text so it becomes unique",
                }),

            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
    }
}
