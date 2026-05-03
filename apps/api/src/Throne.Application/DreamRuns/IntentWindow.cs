namespace Throne.Application.DreamRuns;

/// <summary>
/// Snapshot of intents that have qa or review activity recorded against them and are
/// thus eligible to feed /dream learning. There is no time window — once a user runs
/// /dream, every intent with at least one qa/review goes in (minus those locked by
/// pending DreamRuns or already consumed by a closed processed run).
/// Each <see cref="IntentInWindow"/> carries the full training payload — text history,
/// current text, all qa, all reviews.
/// </summary>
public sealed record IntentWindow(IReadOnlyList<IntentInWindow> Items);

public sealed record IntentInWindow(
    string IntentId,
    string CurrentText,
    IReadOnlyList<IntentTextVersionSnapshot> TextVersions,
    IReadOnlyList<IntentQaSnapshot> QaList,
    IReadOnlyList<IntentReviewSnapshot> ReviewList,
    DateTimeOffset UpdatedAt);

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
