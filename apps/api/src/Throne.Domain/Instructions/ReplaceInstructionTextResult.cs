using Throne.Domain.TextVersions;

namespace Throne.Domain.Instructions;

public abstract record ReplaceInstructionTextResult
{
    private ReplaceInstructionTextResult() { }

    public sealed record Replaced(TextVersion Version) : ReplaceInstructionTextResult;

    public sealed record MatchNotFound(string QueryPreview) : ReplaceInstructionTextResult;

    public sealed record MatchAmbiguous(int MatchesCount, IReadOnlyList<int> MatchLines) : ReplaceInstructionTextResult;
}
