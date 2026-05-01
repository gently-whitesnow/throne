using Throne.Domain.TextVersions;

namespace Throne.Domain.Intents;

public abstract record InsertTextResult
{
    private InsertTextResult() { }

    public sealed record Inserted(TextVersion Version) : InsertTextResult;

    public sealed record LineOutOfRange(int TotalLines, int RequestedAfterLine) : InsertTextResult;
}
