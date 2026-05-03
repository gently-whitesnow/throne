namespace Throne.Domain.DreamRuns;

/// <summary>
/// Snapshot reference to an Intent that contributed to a DreamRun's training context.
/// Carries the per-intent token count (cl100k_base, sum of `Intent.text` history + final text +
/// all `intent_qa` + all `intent_review` for that intent) and the moment of snapshotting.
/// </summary>
public sealed record IntentRef(string IntentId, int TokenCount, DateTimeOffset SnapshottedAt)
{
    public static IntentRef Create(string intentId, int tokenCount, DateTimeOffset snapshottedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentOutOfRangeException.ThrowIfNegative(tokenCount);
        return new IntentRef(intentId, tokenCount, snapshottedAt);
    }
}
