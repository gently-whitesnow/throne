using Throne.Application.Events;
using Throne.Domain.Repositories;

namespace Throne.Application.Ports;

/// <summary>
/// Persistence boundary for one-shot pull request artifacts (ADR-0031).
/// A write is an idempotent latest-wins upsert per <c>(binding_id, type)</c>.
/// </summary>
public interface IPullRequestArtifactRepository
{
    Task<WritePullRequestArtifactOutcome> UpsertAsync(
        BindingId bindingId,
        int pullRequestNumber,
        string type,
        string render,
        string content,
        string summary,
        string source,
        IReadOnlyList<string> sourceRefs,
        DateTimeOffset producedAt,
        CancellationToken ct);

    Task<PullRequestArtifact?> GetAsync(BindingId bindingId, string type, CancellationToken ct);

    Task<IReadOnlyList<PullRequestArtifact>> ListAsync(BindingId bindingId, CancellationToken ct);
}

public sealed record WritePullRequestArtifactOutcome(PullRequestArtifact Artifact, bool Created) : IDomainEventCarrier
{
    public IReadOnlyList<IDomainEvent> Events => [new PullRequestArtifactUpdated(Artifact)];
}
