using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Instructions;
using Throne.Domain.TextVersions;

namespace Throne.Application.Instructions;

public sealed record ReplaceInstructionTextCommand(
    string InstructionId,
    int ExpectedVersion,
    string OldText,
    string NewText,
    TextVersionAuthor Author);

public sealed class ReplaceInstructionTextHandler(
    IInstructionRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Instruction> HandleAsync(ReplaceInstructionTextCommand command, CancellationToken ct)
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

        var id = new InstructionId(command.InstructionId);
        var now = clock.GetUtcNow();

        var outcome = await unitOfWork.ExecuteAsync(
            inner => repository.ReplaceTextAsync(id, command.ExpectedVersion, command.OldText, command.NewText, command.Author, now, inner),
            ct).ConfigureAwait(false);

        return outcome switch
        {
            ReplaceInstructionTextOutcome.Replaced replaced => replaced.Instruction,

            ReplaceInstructionTextOutcome.NotFound => throw new ApiException(
                ErrorCodes.InstructionNotFound,
                $"Instruction '{command.InstructionId}' not found.",
                new Dictionary<string, object?> { ["instruction_id"] = command.InstructionId }),

            ReplaceInstructionTextOutcome.VersionConflict conflict => throw new ApiException(
                ErrorCodes.InstructionVersionConflict,
                "Instruction.current_version does not match expected_version.",
                new Dictionary<string, object?>
                {
                    ["instruction_id"] = command.InstructionId,
                    ["expected_version"] = command.ExpectedVersion,
                    ["current_version"] = conflict.CurrentVersion,
                }),

            ReplaceInstructionTextOutcome.MatchNotFound notFound => throw new ApiException(
                ErrorCodes.InstructionTextMatchNotFound,
                "old_text was not found in Instruction.text.",
                new Dictionary<string, object?>
                {
                    ["instruction_id"] = command.InstructionId,
                    ["query_preview"] = notFound.QueryPreview,
                    ["hint"] = "extend old_text with neighbouring context to make it unique",
                }),

            ReplaceInstructionTextOutcome.MatchAmbiguous ambiguous => throw new ApiException(
                ErrorCodes.InstructionTextMatchAmbiguous,
                "old_text matches more than one location in Instruction.text.",
                new Dictionary<string, object?>
                {
                    ["instruction_id"] = command.InstructionId,
                    ["matches_count"] = ambiguous.MatchesCount,
                    ["match_lines"] = ambiguous.MatchLines,
                    ["hint"] = "extend old_text so it becomes unique",
                }),

            _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
        };
    }
}
