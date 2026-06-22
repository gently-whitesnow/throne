using Throne.Api.Repositories;
using Throne.Application.Events;
using Throne.Realtime.Contracts;
using Throne.Realtime.Contracts.Generated;

namespace Throne.Api.Realtime;

/// <summary>
/// Maps the knowledge-page domain event (ADR-0031) onto the contract-first realtime
/// envelope declared in <c>specs/contracts/realtime/events.yaml</c>:
/// <c>repository.document_updated</c> — pointer-only <c>{ provider, owner, repo, slug, version }</c>.
/// The document body and version timeline are refetched by the client, so the page-write frame
/// stays a pointer (Q2 of the slice interview).
/// </summary>
internal static class RepositoryRegistryRealtimeMapper
{
    public static RealtimeEventEnvelope? TryMap(IDomainEvent evt) => evt switch
    {
        RepositoryDocumentUpdated updated => new RealtimeEventEnvelope(
            RealtimeEventNames.RepositoryDocumentUpdated,
            new
            {
                // Coordinate.Provider already holds the wire string (GitProviderNames); emitting
                // it as-is avoids the generated GitProvider enum, whose JsonStringEnumConverter is
                // attached per-property and would serialise as int inside an anonymous object
                // (same reasoning as IntentRepositoryRealtimeMapper's clone-status payload).
                provider = updated.Artifact.Coordinate.Provider,
                owner = updated.Artifact.Coordinate.Owner,
                repo = updated.Artifact.Coordinate.Repo,
                slug = updated.Artifact.Slug,
                version = updated.Artifact.Version,
            }),
        _ => null,
    };
}
