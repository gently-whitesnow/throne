using Throne.Domain.Repositories;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Mappers;

internal static class PullRequestArtifactRowMapper
{
    public static PullRequestArtifactRow ToRow(PullRequestArtifact artifact) => new()
    {
        Id = artifact.Id.Value,
        BindingId = artifact.BindingId.Value,
        PullRequestNumber = artifact.PullRequestNumber,
        Type = artifact.Type,
        Render = artifact.Render,
        Content = artifact.Content,
        Summary = artifact.Summary,
        Source = artifact.Source,
        SourceRefs = artifact.SourceRefs.ToList(),
        ProducedAt = artifact.ProducedAt,
        HeadSha = artifact.HeadSha,
        ReviewRecommendation = ToPayload(artifact.ReviewRecommendation),
    };

    public static PullRequestArtifact ToDomain(PullRequestArtifactRow row) =>
        PullRequestArtifact.Restore(new PullRequestArtifactSnapshot(
            Id: new PullRequestArtifactId(row.Id),
            BindingId: new BindingId(row.BindingId),
            PullRequestNumber: row.PullRequestNumber,
            Type: row.Type,
            Render: row.Render,
            Content: row.Content,
            Summary: row.Summary,
            Source: row.Source,
            SourceRefs: row.SourceRefs,
            ProducedAt: row.ProducedAt,
            HeadSha: row.HeadSha,
            ReviewRecommendation: ToDomain(row.ReviewRecommendation)));

    public static ReviewRecommendationPayload? ToPayload(ReviewRecommendation? recommendation) =>
        recommendation is null
            ? null
            : new ReviewRecommendationPayload
            {
                FileOrder = recommendation.FileOrder
                    .Select(entry => new ReviewFileOrderEntryPayload
                    {
                        Path = entry.Path,
                        Reason = entry.Reason,
                        Risk = entry.Risk.HasValue ? ReviewFileRiskNames.ToWire(entry.Risk.Value) : null,
                    })
                    .ToList(),
            };

    private static ReviewRecommendation? ToDomain(ReviewRecommendationPayload? payload) =>
        payload is null
            ? null
            : ReviewRecommendation.Create(payload.FileOrder
                .Select(entry => new ReviewFileOrderEntry(
                    entry.Path,
                    entry.Reason,
                    ReviewFileRiskNames.TryParse(entry.Risk)))
                .ToList());
}
