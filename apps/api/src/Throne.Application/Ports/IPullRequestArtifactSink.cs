using Throne.Application.Repositories;
using Throne.Domain.Repositories;

namespace Throne.Application.Ports;

/// <summary>Idempotent ingest boundary for pull request artifacts.</summary>
public interface IPullRequestArtifactSink
{
    Task<PullRequestArtifact> IngestAsync(WritePullRequestArtifactCommand command, CancellationToken ct);
}
