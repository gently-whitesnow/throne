using Throne.Domain.Repositories;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Repositories;

internal static class PullRequestArtifactDocumentMapper
{
    public static PullRequestArtifactDocument ToDocument(PullRequestArtifact artifact) => new()
    {
        Id = artifact.Id.Value,
        BindingId = artifact.BindingId.Value,
        PullRequestNumber = artifact.PullRequestNumber,
        Type = artifact.Type,
        Render = artifact.Render,
        Content = artifact.Content,
        Summary = artifact.Summary,
        Source = artifact.Source,
        SourceRefs = artifact.SourceRefs.ToArray(),
        ProducedAt = artifact.ProducedAt.UtcDateTime,
        HeadSha = artifact.HeadSha,
        ReviewRecommendation = ToDocument(artifact.ReviewRecommendation),
    };

    public static PullRequestArtifact ToDomain(PullRequestArtifactDocument doc) =>
        PullRequestArtifact.Restore(new PullRequestArtifactSnapshot(
            Id: new PullRequestArtifactId(doc.Id),
            BindingId: new BindingId(doc.BindingId),
            PullRequestNumber: doc.PullRequestNumber,
            Type: doc.Type,
            Render: doc.Render,
            Content: doc.Content,
            Summary: doc.Summary,
            Source: doc.Source,
            SourceRefs: doc.SourceRefs,
            ProducedAt: ToUtc(doc.ProducedAt),
            HeadSha: doc.HeadSha,
            ReviewRecommendation: ToDomain(doc.ReviewRecommendation)));

    public static ReviewRecommendationDocument? ToDocument(ReviewRecommendation? recommendation) =>
        recommendation is null
            ? null
            : new ReviewRecommendationDocument
            {
                FileOrder = recommendation.FileOrder
                    .Select(entry => new ReviewFileOrderEntryDocument
                    {
                        Path = entry.Path,
                        Reason = entry.Reason,
                        Risk = entry.Risk.HasValue ? ReviewFileRiskNames.ToWire(entry.Risk.Value) : null,
                    })
                    .ToArray(),
            };

    private static ReviewRecommendation? ToDomain(ReviewRecommendationDocument? doc) =>
        doc is null
            ? null
            : ReviewRecommendation.Create(doc.FileOrder
                .Select(entry => new ReviewFileOrderEntry(
                    entry.Path,
                    entry.Reason,
                    ReviewFileRiskNames.TryParse(entry.Risk)))
                .ToArray());

    private static DateTimeOffset ToUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
