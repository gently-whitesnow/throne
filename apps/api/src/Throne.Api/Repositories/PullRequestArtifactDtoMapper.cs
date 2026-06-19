using Throne.Domain.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories;

internal static class PullRequestArtifactDtoMapper
{
    public static PullRequestArtifactDto ToDto(PullRequestArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        return new PullRequestArtifactDto
        {
            Id = artifact.Id.Value,
            Binding_id = artifact.BindingId.Value,
            Pull_request_number = artifact.PullRequestNumber,
            Type = artifact.Type,
            Render = RepositoryEnumDtoMapper.ToWireArtifactRender(artifact.Render),
            Content = artifact.Content,
            Summary = artifact.Summary,
            Source = RepositoryEnumDtoMapper.ToWireArtifactSource(artifact.Source),
            Source_refs = artifact.SourceRefs.ToList(),
            Head_sha = artifact.HeadSha,
            Review_recommendation = ReviewRecommendationDtoMapper.ToDto(artifact.ReviewRecommendation),
            Produced_at = artifact.ProducedAt,
        };
    }
}
