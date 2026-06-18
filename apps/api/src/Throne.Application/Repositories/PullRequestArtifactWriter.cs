using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

public sealed record WritePullRequestArtifactCommand(
    BindingId BindingId,
    string Type,
    string Render,
    string Content,
    string Summary,
    string Source,
    IReadOnlyList<string> SourceRefs,
    DateTimeOffset ProducedAt,
    string? HeadSha = null,
    string? ReviewRecommendation = null);

public sealed class PullRequestArtifactWriter(
    IPullRequestArtifactRepository artifacts,
    IIntentRepositoryBindingRepository bindings,
    IUnitOfWork unitOfWork) : IPullRequestArtifactSink
{
    public async Task<PullRequestArtifact> IngestAsync(WritePullRequestArtifactCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var outcome = await unitOfWork.ExecuteAsync(
            async inner =>
            {
                var binding = await bindings.GetByIdAsync(command.BindingId, inner)
                    ?? throw new ApiException(
                        ErrorCodes.RepositoryBindingNotFound,
                        $"Repository binding not found: {command.BindingId.Value}.",
                        new Dictionary<string, object?> { ["binding_id"] = command.BindingId.Value });

                var pullRequestNumber = binding.State.PullRequestNumber
                    ?? throw new ApiException(
                        ErrorCodes.RepositoryPullRequestNotAttached,
                        $"Repository binding has no pull request attached: {command.BindingId.Value}.",
                        new Dictionary<string, object?> { ["binding_id"] = command.BindingId.Value });

                return await artifacts.UpsertAsync(
                    command.BindingId,
                    pullRequestNumber,
                    command.Type,
                    command.Render,
                    command.Content,
                    command.Summary,
                    command.Source,
                    command.SourceRefs,
                    command.ProducedAt,
                    command.HeadSha,
                    command.ReviewRecommendation,
                    inner);
            },
            ct);

        return outcome.Artifact;
    }
}
