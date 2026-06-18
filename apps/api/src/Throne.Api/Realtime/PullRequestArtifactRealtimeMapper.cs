using Throne.Application.Events;
using Throne.Realtime.Contracts;
using Throne.Realtime.Contracts.Generated;

namespace Throne.Api.Realtime;

internal static class PullRequestArtifactRealtimeMapper
{
    public static RealtimeEventEnvelope? TryMap(IDomainEvent evt) => evt switch
    {
        PullRequestArtifactUpdated updated => new RealtimeEventEnvelope(
            RealtimeEventNames.PullRequestArtifactUpdated,
            new
            {
                binding_id = updated.Artifact.BindingId.Value,
                pull_request_number = updated.Artifact.PullRequestNumber,
                type = updated.Artifact.Type,
                produced_at = updated.Artifact.ProducedAt,
            }),
        _ => null,
    };
}
