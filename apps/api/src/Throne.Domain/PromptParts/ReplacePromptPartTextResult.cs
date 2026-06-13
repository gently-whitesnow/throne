using Throne.Domain.TextVersions;

namespace Throne.Domain.PromptParts;

public abstract record ReplacePromptPartTextResult
{
    private ReplacePromptPartTextResult() { }

    public sealed record Replaced(TextVersion Version) : ReplacePromptPartTextResult;

    public sealed record MatchNotFound(string QueryPreview) : ReplacePromptPartTextResult;

    public sealed record MatchAmbiguous(int MatchesCount, IReadOnlyList<int> MatchLines) : ReplacePromptPartTextResult;
}
