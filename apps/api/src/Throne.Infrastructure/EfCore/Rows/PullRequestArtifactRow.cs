namespace Throne.Infrastructure.EfCore.Rows;

/// <summary>
/// Persistence POCO for the <c>pull_request_artifacts</c> table (ADR-0031).
/// Mirrors <c>PullRequestArtifactDocument</c>; <c>source_refs</c> stays a JSON array
/// column (nothing queries inside it) and <c>review_recommendation</c> is the typed
/// subdoc reused from the domain. Latest-write-wins per <c>(binding_id, type)</c>.
/// </summary>
internal sealed class PullRequestArtifactRow
{
    public string Id { get; set; } = string.Empty;
    public string BindingId { get; set; } = string.Empty;
    public int PullRequestNumber { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Render { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public List<string> SourceRefs { get; set; } = [];
    public DateTimeOffset ProducedAt { get; set; }
    public string? HeadSha { get; set; }
    public ReviewRecommendationPayload? ReviewRecommendation { get; set; }
}

/// <summary>JSON shape for <c>review_recommendation</c>. Keeps the per-type payload self-contained.</summary>
internal sealed class ReviewRecommendationPayload
{
    public List<ReviewFileOrderEntryPayload> FileOrder { get; set; } = [];
}

internal sealed class ReviewFileOrderEntryPayload
{
    public string Path { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? Risk { get; set; }
}
