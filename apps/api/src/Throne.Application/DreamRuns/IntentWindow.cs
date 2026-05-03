namespace Throne.Application.DreamRuns;

/// <summary>
/// Snapshot of intents whose qa/review activity falls into the safe time window
/// (see ADR-0011). Each <see cref="IntentInWindow"/> carries the full training
/// payload — text history, current text, all qa, all reviews — that would feed /dream.
/// </summary>
public sealed record IntentWindow(
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    IReadOnlyList<IntentInWindow> Items);

public sealed record IntentInWindow(
    string IntentId,
    string CurrentText,
    IReadOnlyList<IntentTextVersionSnapshot> TextVersions,
    IReadOnlyList<IntentQaSnapshot> QaList,
    IReadOnlyList<IntentReviewSnapshot> ReviewList,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Application-layer projection of one <c>text_versions</c> row that contributes raw
/// text to the token counter.
/// </summary>
public sealed record IntentTextVersionSnapshot(
    int Version,
    string Kind,
    string? Snapshot,
    string? OldText,
    string? NewText,
    string? InsertText)
{
    public string EffectiveText() =>
        Snapshot ?? NewText ?? InsertText ?? OldText ?? string.Empty;
}

public sealed record IntentQaSnapshot(string Id, string Question, string Answer, DateTimeOffset CreatedAt);

public sealed record IntentReviewSnapshot(string Id, string Reason, string Note, DateTimeOffset CreatedAt);
