namespace Throne.Domain.TaskTrackers;

/// <summary>
/// Identifier for an <see cref="IntentCardAttachment"/>. Wire-format is the raw
/// <see cref="Value"/> string (no prefix) — same convention as <c>IntentId</c>.
/// </summary>
public readonly record struct CardAttachmentId(string Value)
{
    public static CardAttachmentId New() => new(Guid.NewGuid().ToString("N"));

    public override string ToString() => Value;
}
