using Throne.Domain.Intents;

namespace Throne.Application.Ports;

public abstract record InsertIntentTextAfterLineOutcome
{
    private InsertIntentTextAfterLineOutcome() { }

    public sealed record Inserted(Intent Intent) : InsertIntentTextAfterLineOutcome;

    public sealed record NotFound : InsertIntentTextAfterLineOutcome;

    public sealed record VersionConflict(int CurrentVersion) : InsertIntentTextAfterLineOutcome;

    public sealed record LineOutOfRange(int TotalLines, int RequestedAfterLine) : InsertIntentTextAfterLineOutcome;
}
