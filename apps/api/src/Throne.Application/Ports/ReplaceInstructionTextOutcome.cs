using Throne.Domain.Instructions;

namespace Throne.Application.Ports;

public abstract record ReplaceInstructionTextOutcome
{
    private ReplaceInstructionTextOutcome() { }

    public sealed record Replaced(Instruction Instruction) : ReplaceInstructionTextOutcome;

    public sealed record NotFound : ReplaceInstructionTextOutcome;

    public sealed record VersionConflict(int CurrentVersion) : ReplaceInstructionTextOutcome;

    public sealed record MatchNotFound(string QueryPreview) : ReplaceInstructionTextOutcome;

    public sealed record MatchAmbiguous(int MatchesCount, IReadOnlyList<int> MatchLines) : ReplaceInstructionTextOutcome;
}
