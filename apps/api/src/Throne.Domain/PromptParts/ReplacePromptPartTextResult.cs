namespace Throne.Domain.PromptParts;

public abstract record ReplacePromptPartTextResult
{
    private ReplacePromptPartTextResult() { }

    public sealed record Replaced : ReplacePromptPartTextResult;

    public sealed record MatchNotFound(string QueryPreview) : ReplacePromptPartTextResult;

    public sealed record MatchAmbiguous(int MatchesCount, IReadOnlyList<int> MatchLines) : ReplacePromptPartTextResult;
}
