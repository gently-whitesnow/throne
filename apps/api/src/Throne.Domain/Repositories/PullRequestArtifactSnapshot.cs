namespace Throne.Domain.Repositories;

/// <summary>Flat rehydration payload for <see cref="PullRequestArtifact.Restore"/>.</summary>
public sealed record PullRequestArtifactSnapshot(
    PullRequestArtifactId Id,
    BindingId BindingId,
    int PullRequestNumber,
    string Type,
    string Render,
    string Content,
    string Summary,
    string Source,
    IReadOnlyList<string> SourceRefs,
    DateTimeOffset ProducedAt,
    string? HeadSha = null,
    ReviewRecommendation? ReviewRecommendation = null);
