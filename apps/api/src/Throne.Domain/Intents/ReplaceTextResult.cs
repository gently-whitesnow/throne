using Throne.Domain.TextVersions;

namespace Throne.Domain.Intents;

public abstract record ReplaceTextResult
{
    private ReplaceTextResult() { }

    public sealed record Replaced(TextVersion Version) : ReplaceTextResult;

    public sealed record MatchNotFound(string QueryPreview) : ReplaceTextResult;

    public sealed record MatchAmbiguous(int MatchesCount, IReadOnlyList<int> MatchLines) : ReplaceTextResult;
}
