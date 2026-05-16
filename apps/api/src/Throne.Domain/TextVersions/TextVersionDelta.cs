namespace Throne.Domain.TextVersions;

public sealed record TextVersionDelta(
    string? Snapshot,
    string? OldText,
    string? NewText,
    int? AfterLine,
    string? InsertText)
{
    public static TextVersionDelta None { get; } = new(null, null, null, null, null);
}
