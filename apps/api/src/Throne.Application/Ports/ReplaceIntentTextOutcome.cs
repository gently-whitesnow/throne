using Throne.Domain.Intents;

namespace Throne.Application.Ports;

public abstract record ReplaceIntentTextOutcome
{
    private ReplaceIntentTextOutcome() { }

    public sealed record Replaced(Intent Intent) : ReplaceIntentTextOutcome;

    public sealed record NotFound : ReplaceIntentTextOutcome;

    public sealed record VersionConflict(int CurrentVersion) : ReplaceIntentTextOutcome;

    public sealed record MatchNotFound(string QueryPreview) : ReplaceIntentTextOutcome;

    public sealed record MatchAmbiguous(int MatchesCount, IReadOnlyList<int> MatchLines) : ReplaceIntentTextOutcome;
}
