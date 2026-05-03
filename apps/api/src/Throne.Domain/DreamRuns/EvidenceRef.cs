namespace Throne.Domain.DreamRuns;

/// <summary>
/// Точечная ссылка на сырой evidence-документ (не на Intent),
/// который попал в окно DreamRun. Kind — один из <see cref="EvidenceKindNames"/>,
/// Id — id исходной записи (intent_review.id, intent_qa.id, mcp_call_log.id и т.д.).
/// CreatedAt опциональный — нужен UI/debug; помогает избежать дополнительного lookup.
/// </summary>
public sealed record EvidenceRef(string Kind, string Id, DateTimeOffset? CreatedAt = null)
{
    public static EvidenceRef Create(string kind, string id, DateTimeOffset? createdAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!EvidenceKindNames.IsKnown(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), $"Unknown evidence kind: {kind}.");
        }

        return new EvidenceRef(kind, id, createdAt);
    }
}
